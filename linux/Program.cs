using System.IO;

namespace KamuiT;

internal static class Program
{
    private const string Version = "0.3.0";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            Console.WriteLine("""
                KamuiT Linux — workspace de agentes (GTK4 + VTE)

                Uso:
                  KamuiT
                  KamuiT open grok [-C <cwd>] [-n <count>]
                  KamuiT list | show | ping | agents
                  KamuiT --version

                Dependências de runtime: GTK 4 e libvte-2.91-gtk4 (ou libvte-2.91).
                Ver scripts/install-linux.sh.
                """);
            return 0;
        }

        if (args.Length == 1 && args[0] is "-v" or "--version")
        {
            Console.WriteLine("KamuiT " + Version + " (linux)");
            return 0;
        }

        if (CommandClient.IsServerUp(300))
        {
            var req = KamuiRequest.ParseCli(args);
            var resp = CommandClient.TrySend(req, 3000);
            if (resp is null)
            {
                Console.Error.WriteLine("KamuiT já está aberto, mas o socket não respondeu.");
                return 2;
            }
            if (!resp.Ok)
            {
                Console.Error.WriteLine(resp.Error ?? "erro");
                return 1;
            }
            if (!string.IsNullOrEmpty(resp.Message))
                Console.WriteLine(resp.Message);
            return 0;
        }

        FileStream? lockFile = TryLock();
        if (lockFile is null)
        {
            Thread.Sleep(400);
            if (CommandClient.IsServerUp(500))
            {
                CommandClient.TrySend(KamuiRequest.ParseCli(args), 3000);
                return 0;
            }
        }

        try
        {
            VteNative.Load();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        LinuxPaths.InstallShellInit();
        AgentReadyLinux.InstallHooks();

        var application = Gtk.Application.New("app.kamuit.KamuiT", Gio.ApplicationFlags.FlagsNone);
        application.OnActivate += (sender, _) =>
        {
            var app = (Gtk.Application)sender!;
            var ws = new Workspace(app, args);
            ws.Present();
        };

        try
        {
            return application.RunWithSynchronizationContext(null);
        }
        finally
        {
            lockFile?.Dispose();
        }
    }

    private static FileStream? TryLock()
    {
        try
        {
            var dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? Path.GetTempPath();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "kamuit.lock");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
