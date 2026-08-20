using System.IO;
using System.Threading;
using System.Windows;

namespace KamuiT;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance: 2ª abertura só acorda a janela (ou reencaminha args via pipe).
        const string mutexName = "Local\\KamuiT-SingleInstance";
        _mutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            ForwardToRunningInstance(e.Args);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("DispatcherUnhandledException", args.Exception);
            args.Handled = true; // deixa o usuario continuar; se for fatal o processo cai mesmo
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            LogCrash("UnhandledException", args.ExceptionObject as Exception);
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        try { _mutex?.Dispose(); } catch { }
        _mutex = null;
        base.OnExit(e);
    }

    private static void ForwardToRunningInstance(string[] args)
    {
        try
        {
            // Espera o pipe da instância viva (até ~2s).
            for (var i = 0; i < 20; i++)
            {
                if (CommandClient.IsServerUp(100))
                    break;
                Thread.Sleep(100);
            }

            KamuiRequest req;
            if (args.Length > 0)
                req = ParseArgs(args);
            else
                req = new KamuiRequest { Op = "show" };

            CommandClient.TrySend(req, timeoutMs: 3000);
        }
        catch (Exception ex)
        {
            LogCrash("ForwardToRunningInstance", ex);
        }
    }

    /// <summary>
    /// CLI embutida quando alguém lança KamuiT.exe open grok -C path …
    /// </summary>
    internal static KamuiRequest ParseArgs(string[] args)
    {
        if (args.Length == 0)
            return new KamuiRequest { Op = "show" };

        var op = args[0].Trim().ToLowerInvariant();
        // "KamuiT.exe grok" atalho
        if (op is "grok" or "claude" or "codex" or "pi" or "jcode" or "shell")
            return new KamuiRequest { Op = "open", Agent = op, Count = 1, Show = true };

        if (op is not ("open" or "new" or "tab" or "list" or "show" or "focus" or "type" or "ping" or "close" or "agents"))
            return new KamuiRequest { Op = "show" };

        var req = new KamuiRequest { Op = op, Show = true };
        for (var i = 1; i < args.Length; i++)
        {
            var a = args[i];
            if ((a is "-a" or "--agent" or "-agent") && i + 1 < args.Length)
                req.Agent = args[++i];
            else if ((a is "-C" or "--cwd" or "-cwd" or "--dir") && i + 1 < args.Length)
                req.Cwd = args[++i];
            else if ((a is "-n" or "--count" or "-count") && i + 1 < args.Length && int.TryParse(args[++i], out var n))
                req.Count = n;
            else if ((a is "-s" or "--slot" or "-slot") && i + 1 < args.Length && int.TryParse(args[++i], out var s))
                req.Slot = s;
            else if ((a is "-t" or "--text" or "-text") && i + 1 < args.Length)
                req.Text = args[++i];
            else if (a is "--enter" or "-enter")
                req.Enter = true;
            else if (a is "--no-show")
                req.Show = false;
            else if (a.StartsWith('-'))
                continue;
            else if ((req.Op is "open" or "new" or "tab") && req.Agent is null)
                req.Agent = a; // kamuit open grok
            else if ((req.Op is "focus" or "close") && req.Slot is null && int.TryParse(a, out var slot))
                req.Slot = slot;
            else if (req.Op is "type" && req.Text is null)
                req.Text = a;
        }
        return req;
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kamuit");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n");
        }
        catch { }
    }
}
