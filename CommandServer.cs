using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;

namespace KamuiT;

/// <summary>
/// Named pipe server — cada linha JSON é um request, resposta é uma linha JSON.
/// Windows: <c>\\.\pipe\kamuit</c>. Linux: socket em <c>$XDG_RUNTIME_DIR/kamuit.sock</c>.
/// Roda em background; handlers no UI thread via <paramref name="invokeOnUi"/>.
/// </summary>
public sealed class CommandServer : IDisposable
{
    public static string PipeName { get; } = OperatingSystem.IsWindows()
        ? "kamuit"
        : Path.Combine(
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
            ?? Path.GetTempPath(),
            "kamuit.sock");

    private readonly Func<Func<KamuiResponse>, Task<KamuiResponse>> _invokeOnUi;
    private readonly Func<KamuiRequest, KamuiResponse> _handler;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public CommandServer(
        Func<Func<KamuiResponse>, Task<KamuiResponse>> invokeOnUi,
        Func<KamuiRequest, KamuiResponse> handler)
    {
        _invokeOnUi = invokeOnUi;
        _handler = handler;
    }

    public void Start()
    {
        if (_loop is not null)
            return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        _loop = null;
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            await ListenUnixAsync(ct).ConfigureAwait(false);
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                var connected = pipe;
                pipe = null; // ownership transferred
                _ = Task.Run(async () =>
                {
                    using (connected)
                        await HandleStream(connected, ct).ConfigureAwait(false);
                }, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                if (pipe is not null)
                {
                    try { pipe.Dispose(); } catch { }
                }
            }
        }
    }

    private async Task ListenUnixAsync(CancellationToken ct)
    {
        try { File.Delete(PipeName); } catch { /* stale socket */ }
        var dir = Path.GetDirectoryName(PipeName);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(PipeName));
        listener.Listen(16);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => HandleUnixClient(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleUnixClient(Socket client, CancellationToken ct)
    {
        try
        {
            using (client)
            await using (var stream = new NetworkStream(client, ownsSocket: false))
                await HandleStream(stream, ct).ConfigureAwait(false);
        }
        catch
        {
            // client disconnect — ignore
        }
    }

    private async Task HandleStream(Stream stream, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                await writer.WriteLineAsync(KamuiJson.Serialize(KamuiResponse.Fail("empty request"))).ConfigureAwait(false);
                return;
            }

            KamuiResponse response;
            try
            {
                var req = KamuiJson.Deserialize<KamuiRequest>(line)
                          ?? new KamuiRequest { Op = "" };
                // UI thread — tabs (WPF Dispatcher ou Gtk SynchronizationContext)
                response = await _invokeOnUi(() =>
                {
                    try { return _handler(req); }
                    catch (Exception ex) { return KamuiResponse.Fail(ex.Message); }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                response = KamuiResponse.Fail("bad json: " + ex.Message);
            }

            await writer.WriteLineAsync(KamuiJson.Serialize(response)).ConfigureAwait(false);
        }
        catch
        {
            // client disconnect / pipe broken — ignore
        }
    }
}

/// <summary>Cliente síncrono pro pipe (segunda instância do app / tools).</summary>
public static class CommandClient
{
    public static KamuiResponse? TrySend(KamuiRequest request, int timeoutMs = 2500)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return TrySendUnix(request, timeoutMs);

            using var pipe = new NamedPipeClientStream(
                ".", CommandServer.PipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(timeoutMs);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            writer.WriteLine(KamuiJson.Serialize(request));
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                return KamuiResponse.Fail("empty response");
            return KamuiJson.Deserialize<KamuiResponse>(line) ?? KamuiResponse.Fail("bad response");
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (Exception ex)
        {
            return KamuiResponse.Fail(ex.Message);
        }
    }

    public static bool IsServerUp(int timeoutMs = 400)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                using var sock = ConnectUnix(timeoutMs);
                return sock is not null;
            }

            using var pipe = new NamedPipeClientStream(
                ".", CommandServer.PipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(timeoutMs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static KamuiResponse? TrySendUnix(KamuiRequest request, int timeoutMs)
    {
        using var sock = ConnectUnix(timeoutMs);
        if (sock is null)
            return null;
        using var stream = new NetworkStream(sock, ownsSocket: false);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        writer.WriteLine(KamuiJson.Serialize(request));
        var line = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
            return KamuiResponse.Fail("empty response");
        return KamuiJson.Deserialize<KamuiResponse>(line) ?? KamuiResponse.Fail("bad response");
    }

    private static Socket? ConnectUnix(int timeoutMs)
    {
        var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            sock.ReceiveTimeout = timeoutMs;
            sock.SendTimeout = timeoutMs;
            sock.Connect(new UnixDomainSocketEndPoint(CommandServer.PipeName));
            return sock;
        }
        catch
        {
            sock.Dispose();
            return null;
        }
    }
}
