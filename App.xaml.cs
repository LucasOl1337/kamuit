using System.IO;
using System.Windows;

namespace KamuiT;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
