using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows.Threading;

namespace KamuiT;

/// <summary>
/// Named pipe server <c>\\.\pipe\kamuit</c> — cada linha JSON é um request,
/// resposta é uma linha JSON. Roda em background; handlers no UI thread.
/// </summary>
public sealed class CommandServer : IDisposable
{
    public const string PipeName = "kamuit";

    private readonly Dispatcher _dispatcher;
    private readonly Func<KamuiRequest, KamuiResponse> _handler;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public CommandServer(Dispatcher dispatcher, Func<KamuiRequest, KamuiResponse> handler)
    {
        _dispatcher = dispatcher;
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
                _ = Task.Run(() => HandleClient(pipe, ct), ct);
                pipe = null; // ownership transferred
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

    private async Task HandleClient(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            using (pipe)
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

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
                    // UI thread — tabs/WPF
                    response = await _dispatcher.InvokeAsync(() =>
                    {
                        try { return _handler(req); }
                        catch (Exception ex) { return KamuiResponse.Fail(ex.Message); }
                    }).Task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    response = KamuiResponse.Fail("bad json: " + ex.Message);
                }

                await writer.WriteLineAsync(KamuiJson.Serialize(response)).ConfigureAwait(false);
            }
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
}
