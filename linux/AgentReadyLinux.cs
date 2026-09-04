using System.Diagnostics;

namespace KamuiT;

/// <summary>
/// Toca SoundEffects/Terminal{N}.mp3 quando um agente sinaliza Stop.
/// No Linux usa ffplay/mpg123/gst-play/paplay — sem WPF MediaPlayer.
/// </summary>
internal sealed class AgentReadyLinux
{
    private static readonly string SignalsDir = Path.Combine(LinuxPaths.Home, ".kamuit", "signals");
    private static readonly string SoundsDir = Path.Combine(AppContext.BaseDirectory, "SoundEffects");

    private readonly Action<Action> _invokeOnUi;
    private readonly Func<string?, int?, int?> _resolveSlot;
    private FileSystemWatcher? _watcher;

    public AgentReadyLinux(Action<Action> invokeOnUi, Func<string?, int?, int?> resolveSlot)
    {
        _invokeOnUi = invokeOnUi;
        _resolveSlot = resolveSlot;
    }

    public void Start()
    {
        Directory.CreateDirectory(SignalsDir);
        _watcher = new FileSystemWatcher(SignalsDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Created += (_, e) => HandleSignal(e.FullPath);
        _watcher.Renamed += (_, e) => HandleSignal(e.FullPath);
        foreach (var pending in Directory.EnumerateFiles(SignalsDir, "*.json"))
            HandleSignal(pending);
    }

    private void HandleSignal(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? tabId = null;
            if (root.TryGetProperty("tabId", out var tabIdProp) && tabIdProp.ValueKind == System.Text.Json.JsonValueKind.String)
                tabId = tabIdProp.GetString();

            int? legacyTab = null;
            if (root.TryGetProperty("tab", out var tabProp) && tabProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                legacyTab = tabProp.GetInt32();

            _invokeOnUi(() =>
            {
                var slot = _resolveSlot(tabId, legacyTab);
                if (slot is int n)
                    Play(n);
            });
        }
        catch { }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void Play(int tab)
    {
        var file = Path.Combine(SoundsDir, $"Terminal{tab}.mp3");
        if (!File.Exists(file))
            return;

        var players = new (string FileName, string Args)[]
        {
            ("ffplay", $"-nodisp -autoexit -loglevel quiet \"{file}\""),
            ("mpg123", $"-q \"{file}\""),
            ("gst-play-1.0", $"--no-video \"{file}\""),
            ("paplay", $"\"{file}\""),
            ("pw-play", $"\"{file}\""),
        };
        foreach (var (name, args) in players)
        {
            try
            {
                var psi = new ProcessStartInfo(name, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                Process.Start(psi);
                return;
            }
            catch { /* tenta o próximo player */ }
        }
    }

    public static void InstallHooks()
    {
        try
        {
            var script = Path.Combine(AppContext.BaseDirectory, "scripts", "install-agent-ready-hooks.mjs");
            if (!File.Exists(script))
                return;
            Process.Start(new ProcessStartInfo("node", $"\"{script}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch { }
    }
}
