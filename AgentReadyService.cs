using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace KamuiT;

/// <summary>
/// Toca SoundEffects/Terminal{N}.mp3 quando um agente sinaliza que terminou
/// (hook Stop -> ~/.kamuit/signals/*.json -> FileSystemWatcher aqui).
/// N é a posição visual atual da aba (resolvida via tabId), não o slot de criação.
/// </summary>
public sealed class AgentReadyService
{
    private static readonly string SignalsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kamuit", "signals");

    private static readonly string SoundsDir = Path.Combine(AppContext.BaseDirectory, "SoundEffects");

    private readonly Action<Action> _invokeOnUi;
    private readonly Func<string?, int?, int?> _resolveSlot;
    private FileSystemWatcher? _watcher;
    private MediaPlayer? _player;

    /// <param name="invokeOnUi">marshal pra UI thread (MediaPlayer exige)</param>
    /// <param name="resolveSlot">
    /// (tabId, legacyTab) → slot visual 1-based atual, ou null para não tocar.
    /// </param>
    public AgentReadyService(Action<Action> invokeOnUi, Func<string?, int?, int?> resolveSlot)
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
        // o script escreve .tmp e renomeia -> chega como Renamed (criação direta vem como Created)
        _watcher.Created += (_, e) => HandleSignal(e.FullPath);
        _watcher.Renamed += (_, e) => HandleSignal(e.FullPath);

        // sinais que chegaram com o app fechado nunca disparam evento — varre os pendentes
        foreach (var pending in Directory.EnumerateFiles(SignalsDir, "*.json"))
            HandleSignal(pending);
    }

    private void HandleSignal(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? tabId = null;
            if (root.TryGetProperty("tabId", out var tabIdProp) && tabIdProp.ValueKind == JsonValueKind.String)
                tabId = tabIdProp.GetString();

            int? legacyTab = null;
            if (root.TryGetProperty("tab", out var tabProp) && tabProp.ValueKind == JsonValueKind.Number)
                legacyTab = tabProp.GetInt32();

            // Resolve na UI thread: a coleção de abas é do dispatcher
            _invokeOnUi(() =>
            {
                var slot = _resolveSlot(tabId, legacyTab);
                if (slot is int n)
                    Play(n);
            });
        }
        catch { /* sinal malformado ou arquivo ainda em escrita — ignora */ }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private void Play(int tab)
    {
        var file = Path.Combine(SoundsDir, $"Terminal{tab}.mp3");
        if (!File.Exists(file))
            return; // sem som pra esse slot (ex: aba > 5)

        _player ??= new MediaPlayer();
        _player.Stop();
        _player.Open(new Uri(file));
        _player.Play();
    }

    /// <summary>Instala os hooks Stop do Grok/Claude (idempotente, fire-and-forget).</summary>
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
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch { /* node ausente ou hook falhou — o app funciona sem som */ }
    }
}
