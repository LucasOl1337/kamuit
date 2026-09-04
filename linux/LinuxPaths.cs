namespace KamuiT;

/// <summary>Pastas padrão no Linux (equivalente a C:\projetos no Windows).</summary>
internal static class LinuxPaths
{
    public static string Home { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Desktop { get; } =
        FirstExistingDir(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Home, "Desktop"),
            Path.Combine(Home, "Área de Trabalho"))
        ?? Home;

    public static string ProjectsRoot { get; } =
        FirstExistingDir(
            Path.Combine(Home, "projetos"),
            Path.Combine(Home, "Projetos"),
            Path.Combine(Home, "projects"),
            Path.Combine(Home, "Projects"))
        ?? Home;

    public static string NexUnioRoot { get; } =
        FirstExistingDir(
            Path.Combine(Home, "NexUnio"),
            "/opt/NexUnio")
        ?? Path.Combine(Home, "NexUnio");

    public static string NexSalesPath => Path.Combine(NexUnioRoot, "NexSales");

    public static string SfrResgateDigitalPath =>
        Path.Combine(NexUnioRoot, "sfr-resgate-digital");

    public static string KamuitDir { get; } = Path.Combine(Home, ".kamuit");

    public static string ShellInitPath { get; } =
        Path.Combine(KamuitDir, "kamuit-shell-init.sh");

    public static string Shell { get; } =
        Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } s && File.Exists(s)
            ? s
            : FirstExistingFile("/bin/bash", "/usr/bin/bash", "/bin/sh") ?? "/bin/sh";

    public static void InstallShellInit()
    {
        try
        {
            var src = Path.Combine(AppContext.BaseDirectory, "scripts", "kamuit-shell-init.sh");
            if (!File.Exists(src))
                return;
            Directory.CreateDirectory(KamuitDir);
            File.Copy(src, ShellInitPath, overwrite: true);
        }
        catch { /* init opcional */ }
    }

    public static string[] BuildShellArgv(string? unixLaunchCommand)
    {
        var shell = Shell;
        var name = Path.GetFileName(shell);
        var init = File.Exists(ShellInitPath) ? ShellInitPath : null;

        if (name is "bash")
        {
            if (unixLaunchCommand is null)
                return init is null ? [shell] : [shell, "--rcfile", init];

            var rc = init is null ? "" : $"source '{init.Replace("'", "'\\''")}'; ";
            var reexec = init is null
                ? $"exec '{shell.Replace("'", "'\\''")}'"
                : $"exec '{shell.Replace("'", "'\\''")}' --rcfile '{init.Replace("'", "'\\''")}'";
            return [shell, "-lc", rc + unixLaunchCommand + "; " + reexec];
        }

        if (unixLaunchCommand is null)
            return [shell, "-l"];

        return [shell, "-lc", unixLaunchCommand + "; exec '" + shell.Replace("'", "'\\''") + "'"];
    }

    private static string? FirstExistingDir(params string?[] paths)
    {
        foreach (var p in paths)
        {
            if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                return p;
        }
        return null;
    }

    private static string? FirstExistingFile(params string[] paths)
    {
        foreach (var p in paths)
        {
            if (File.Exists(p))
                return p;
        }
        return null;
    }
}
