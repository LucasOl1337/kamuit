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
    internal static KamuiRequest ParseArgs(string[] args) => KamuiRequest.ParseCli(args);

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
