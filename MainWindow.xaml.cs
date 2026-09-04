using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EasyWindowsTerminalControl;
using Microsoft.Terminal.Wpf;

namespace KamuiT;

public class TermTab : INotifyPropertyChanged
{
    private string _title = "";
    private bool _isActive;
    private int _slot;

    /// <summary>Identidade estável da sessão (hooks / ready-sound). Nunca muda com reorder.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Title
    {
        get => _title;
        set { _title = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive))); }
    }

    /// <summary>Posição visual 1-based (Ctrl+N e Terminal{N}.mp3). Atualiza em close/limbo/drag.</summary>
    public int Slot
    {
        get => _slot;
        set { _slot = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Slot))); }
    }

    /// <summary>Agente pedido na abertura (grok/claude/codex/pi) ou null = só shell.</summary>
    public string? Agent { get; set; }

    /// <summary>cwd de nascimento da aba (project pack / CLI).</summary>
    public string WorkingDirectory { get; init; } = @"C:\projetos";

    public required EasyTerminalControl Control { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class MainWindow : Window
{
    // --- Global hotkey (Ctrl+Space: show/hide de qualquer app) ---
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_TOGGLE = 1;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_SPACE = 0x20;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private IntPtr _hwnd;
    private bool _hotkeyRegistered;
    private DispatcherTimer? _hotkeyRetry;

    // --- Tabs ---
    private readonly ObservableCollection<TermTab> _tabs = new();
    private readonly List<TermTab> _limbo = new();
    private TermTab? _activeTab;
    private AgentReadyService? _agentReady;
    private CommandServer? _commandServer;

    // --- Tab drag-reorder ---
    private const double DragThresholdPx = 4;
    private TermTab? _pressTab;
    private Point _pressPoint;
    private bool _dragging;
    private TermTab? _dragTab;
    private Border? _dragSourceBorder;
    private int _dropIndex = -1;

    // --- Quick CD / project pack ---
    private static readonly string DesktopPath =
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    private static readonly string ProjectsRoot = @"C:\projetos";
    private static readonly string NexUnioRoot = @"C:\NexUnio";
    private static readonly string NexSalesPath = @"C:\NexUnio\NexSales";
    private static readonly string SfrResgateDigitalPath = @"C:\NexUnio\sfr-resgate-digital";

    public MainWindow()
    {
        InitializeComponent();
        TabList.ItemsSource = _tabs;

        _agentReady = new AgentReadyService(
            a => Dispatcher.BeginInvoke(a),
            ResolveReadySlot);
        _agentReady.Start();
        AgentReadyService.InstallHooks();
        InstallShellInitScript();

        _commandServer = new CommandServer(
            f => Dispatcher.InvokeAsync(() => f()).Task,
            HandleCommand);
        _commandServer.Start();

        SourceInitialized += OnSourceInitialized;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // KamuiT.exe open grok -C path … (primeira instância com args)
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (args.Length > 0)
        {
            var req = App.ParseArgs(args);
            if (req.Op is "open" or "new" or "tab")
            {
                HandleCommand(req);
                return;
            }
            if (req.Op is not "show" and not "ping" and not "")
                HandleCommand(req);
        }

        if (_tabs.Count == 0)
            NewTab();
    }

    /// <summary>
    /// Copia scripts/kamuit-shell-init.ps1 → ~/.kamuit/ (Tab autofill, sem menu).
    /// </summary>
    private static void InstallShellInitScript()
    {
        try
        {
            var src = Path.Combine(AppContext.BaseDirectory, "scripts", "kamuit-shell-init.ps1");
            if (!File.Exists(src))
                return;
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kamuit");
            Directory.CreateDirectory(dir);
            File.Copy(src, Path.Combine(dir, "kamuit-shell-init.ps1"), overwrite: true);
        }
        catch { /* init opcional */ }
    }

    /// <summary>
    /// Resolve o N do som pela identidade estável da aba (posição visual atual).
    /// tabId desconhecido / limbo → null (não toca). Sem tabId → fallback legado.
    /// </summary>
    private int? ResolveReadySlot(string? tabId, int? legacyTab)
    {
        if (!string.IsNullOrWhiteSpace(tabId) && Guid.TryParse(tabId, out var id))
        {
            for (var i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == id)
                    return i + 1;
            }
            // Limbo ou aba já fechada: melhor silêncio do que número errado
            return null;
        }

        if (legacyTab is >= 1 and <= 5)
            return legacyTab;
        return null;
    }

    // ===================== Tabs =====================

    /// <param name="workingDirectory">cwd da aba (default C:\projetos).</param>
    /// <param name="agent">grok / claude / codex / pi / jcode / null=shell.</param>
    private TermTab NewTab(string? workingDirectory = null, string? agent = null)
    {
        // Id estável pro hook; Slot visual = posição atual (RefreshSlots).
        // Sanitiza vars do TerminalDE; carrega ~/.kamuit/kamuit-shell-init.ps1 (Tab autofill).
        // workingDirectory: pasta inicial do shell (project pack) — default C:\projetos.
        // agent: após init, lança o TUI (agent-first).
        var id = Guid.NewGuid();
        var slot = _tabs.Count + 1;
        var cwd = string.IsNullOrWhiteSpace(workingDirectory) ? ProjectsRoot : workingDirectory;
        if (!Directory.Exists(cwd))
            cwd = ProjectsRoot;

        var agentId = AgentCatalog.Normalize(agent);
        var launch = AgentCatalog.BuildLaunchSnippet(agentId);
        var agentTail = launch is null ? "" : "; " + launch;

        var control = new EasyTerminalControl
        {
            // Identity: KamuiT embeds WT ConPTY+AtlasEngine, but TUIs (Devin, etc.)
            // whitelist only WT/Git Bash via env heuristics. Advertise xterm + WT_SESSION
            // so they don't false-flag us as classic conhost.
            StartupCommandLine = "pwsh.exe -NoLogo -NoExit -Command \"" +
                "$env:TERMINALDE=$null; $env:TERMINALDE_PTY_ID=$null; " +
                "$env:TERMINALDE_SIGNALS_DIR=$null; $env:TERMINALDE_AGENT=$null; " +
                $"$env:KAMUIT='1'; $env:KAMUIT_TAB_ID='{id}'; $env:KAMUIT_TAB='{slot}'; " +
                (agentId is null ? "" : $"$env:KAMUIT_AGENT='{agentId}'; ") +
                "$env:TERM='xterm-256color'; $env:COLORTERM='truecolor'; " +
                "$env:TERM_PROGRAM='KamuiT'; $env:TERM_PROGRAM_VERSION='0.1.0'; " +
                $"$env:WT_SESSION='{id}'; " +
                "if (Test-Path $env:USERPROFILE\\.kamuit\\kamuit-shell-init.ps1) { " +
                ". $env:USERPROFILE\\.kamuit\\kamuit-shell-init.ps1 }" +
                agentTail + "\"",
            WorkingDirectory = cwd,
            Theme = CreateTheme(),
            // Tab fica no terminal (não foge pra chrome). Win32 mode off: Tab como \t
            // chega limpo no PSReadLine (Win32 mode + inject quebrava o complete).
            InputCapture = EasyTerminalControl.INPUT_CAPTURE.TabKey
                         | EasyTerminalControl.INPUT_CAPTURE.DirectionKeys,
            Win32InputMode = false,
        };
        var folderName = Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var baseTitle = string.IsNullOrEmpty(folderName) ? "PowerShell" : folderName;
        var tab = new TermTab
        {
            Id = id,
            Title = agentId is null ? baseTitle : $"{agentId} · {baseTitle}",
            Control = control,
            Slot = slot,
            Agent = agentId,
            WorkingDirectory = cwd,
        };

        // Título da aba acompanha o que a sessão anuncia via OSC (Grok: "Waiting for response...", etc.)
        var titleScanner = new OscTitleScanner();
        control.ConPTYTerm.InterceptOutputToUITerminal = (ref Span<char> output) =>
        {
            var title = titleScanner.Feed(output);
            if (title is not null)
                Dispatcher.BeginInvoke(() => tab.Title = title);
        };

        TermArea.Children.Add(control); // nunca sai da árvore visual (ver ActivateTab)
        _tabs.Add(tab);
        RefreshSlots();
        ActivateTab(tab);
        return tab;
    }

    /// <summary>Abre <paramref name="count"/> abas já com cwd no projeto (sem cd manual).</summary>
    private void OpenProjectPack(string path, int count, string? agent = null)
    {
        count = Math.Clamp(count, 1, 9);
        for (var i = 0; i < count; i++)
            NewTab(path, agent);
    }

    private void OpenProjectPackPopup()
    {
        var popup = new ProjectPackWindow(ProjectsRoot, (path, count) => OpenProjectPack(path, count))
        {
            Owner = this,
        };
        popup.Show();
    }

    /// <summary>Recalcula Slot 1..N pela ordem visual. Não toca no PTY.</summary>
    private void RefreshSlots()
    {
        for (var i = 0; i < _tabs.Count; i++)
            _tabs[i].Slot = i + 1;
    }

    private void MoveTab(TermTab tab, int newIndex)
    {
        var old = _tabs.IndexOf(tab);
        if (old < 0 || newIndex < 0 || newIndex >= _tabs.Count || old == newIndex)
            return;
        _tabs.Move(old, newIndex);
        RefreshSlots();
        ActivateTab(tab);
    }

    private void ActivateTab(TermTab tab)
    {
        if (_activeTab is not null)
        {
            _activeTab.IsActive = false;
            _activeTab.Control.Visibility = Visibility.Collapsed; // esconde o HWND sem destruí-lo
        }
        _activeTab = tab;
        tab.IsActive = true;
        tab.Control.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(DispatcherPriority.Input,
            () => Keyboard.Focus(tab.Control));
    }

    private void CloseTab(TermTab tab)
    {
        try
        {
            var proc = tab.Control.ConPTYTerm?.Process;
            if (proc is { HasExited: false })
                proc.Kill(EntireProcessTree: true);
            proc?.Dispose();
        }
        catch { /* processo ja morreu ou nunca iniciou */ }

        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        TermArea.Children.Remove(tab.Control); // único caso em que o controle sai da árvore: morte definitiva
        RefreshSlots();

        if (_tabs.Count == 0)
        {
            Close();
            return;
        }
        if (_activeTab == tab)
            ActivateTab(_tabs[Math.Max(0, Math.Min(index, _tabs.Count - 1))]);
    }

    private void CycleTab(int direction)
    {
        if (_activeTab is null || _tabs.Count < 2)
            return;
        var index = _tabs.IndexOf(_activeTab);
        var next = (index + direction + _tabs.Count) % _tabs.Count;
        ActivateTab(_tabs[next]);
    }

    // ===================== Limbo =====================
    // Esconder aba SEM matar o PTY: sai da árvore visual (para de renderizar,
    // custo zero) mas o processo continua vivo. Ctrl+Shift+X manda, Ctrl+Shift+L abre.

    private void SendActiveTabToLimbo()
    {
        if (_activeTab is null)
            return;
        var tab = _activeTab;
        _tabs.Remove(tab);
        tab.Slot = 0;
        _limbo.Add(tab); // o controle continua na TermArea, só Collapsed — PTY vivo, render pausada
        RefreshSlots();

        if (_tabs.Count == 0)
            NewTab(); // janela nunca fica sem aba visível
        else
            ActivateTab(_tabs[^1]);
    }

    private void RestoreFromLimbo(TermTab tab)
    {
        _limbo.Remove(tab);
        _tabs.Add(tab);
        RefreshSlots();
        ActivateTab(tab);
    }

    private void OpenLimboPopup()
    {
        if (_limbo.Count == 0)
            return;
        var popup = new LimboWindow(_limbo, RestoreFromLimbo)
        {
            Owner = this,
        };
        popup.Show(); // não-modal: usuário pode continuar no terminal com o popup aberto
    }

    private void OnNewTabClick(object sender, RoutedEventArgs e) => NewTab();

    private void OnTabHeaderPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;
        // Clique no ✕ não inicia drag/activate do header
        if (e.OriginalSource is DependencyObject src && FindAncestor<Button>(src) is not null)
            return;
        if (sender is not Border border || border.Tag is not TermTab tab)
            return;

        _pressTab = tab;
        _pressPoint = e.GetPosition(this);
        _dragging = false;
        _dragTab = null;
        _dropIndex = -1;
        _dragSourceBorder = border;
        border.CaptureMouse();
        e.Handled = true;
    }

    private void OnTabHeaderPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressTab is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(this);
        if (!_dragging)
        {
            if (Math.Abs(pos.X - _pressPoint.X) < DragThresholdPx &&
                Math.Abs(pos.Y - _pressPoint.Y) < DragThresholdPx)
                return;
            _dragging = true;
            _dragTab = _pressTab;
            if (_dragSourceBorder is not null)
                _dragSourceBorder.Opacity = 0.55;
            Mouse.OverrideCursor = Cursors.SizeAll;
        }

        _dropIndex = GetDropIndexFromMouse(e.GetPosition(TabList));
        HighlightDropIndex(_dropIndex);
    }

    private void OnTabHeaderPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_pressTab is null)
            return;

        var pressed = _pressTab;
        var wasDragging = _dragging;
        var dragTab = _dragTab;
        var dropIndex = _dropIndex;

        ClearDragState();

        if (wasDragging && dragTab is not null && dropIndex >= 0)
            MoveTab(dragTab, dropIndex);
        else if (!wasDragging && pressed is not null)
            ActivateTab(pressed);

        e.Handled = true;
    }

    private void OnTabHeaderLostCapture(object sender, MouseEventArgs e)
    {
        if (_pressTab is null && !_dragging)
            return;
        ClearDragState();
    }

    /// <summary>Índice de inserção pela posição X do cursor sobre a barra de abas.</summary>
    private int GetDropIndexFromMouse(Point posInTabList)
    {
        if (_tabs.Count == 0)
            return 0;

        for (var i = 0; i < _tabs.Count; i++)
        {
            var border = GetTabHeaderBorder(_tabs[i]);
            if (border is null)
                continue;
            var origin = border.TranslatePoint(new Point(0, 0), TabList);
            var midX = origin.X + border.ActualWidth / 2;
            if (posInTabList.X < midX)
                return i;
        }
        return _tabs.Count - 1;
    }

    private Border? GetTabHeaderBorder(TermTab tab)
    {
        if (TabList.ItemContainerGenerator.ContainerFromItem(tab) is not FrameworkElement container)
            return null;
        return FindDescendant<Border>(container, b => ReferenceEquals(b.Tag, tab));
    }

    private void HighlightDropIndex(int index)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            var border = GetTabHeaderBorder(_tabs[i]);
            if (border is null || ReferenceEquals(border, _dragSourceBorder))
                continue;
            if (i == index && _dragTab is not null && !ReferenceEquals(_tabs[i], _dragTab))
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
            else
                border.ClearValue(Border.BorderBrushProperty);
        }
    }

    private void ClearDragHighlights()
    {
        foreach (var tab in _tabs)
        {
            var border = GetTabHeaderBorder(tab);
            if (border is null || ReferenceEquals(border, _dragSourceBorder))
                continue;
            border.ClearValue(Border.BorderBrushProperty);
        }
    }

    private void ClearDragState()
    {
        ClearDragHighlights();
        _dragSourceBorder?.ReleaseMouseCapture();
        if (_dragSourceBorder is not null)
            _dragSourceBorder.Opacity = 1;
        Mouse.OverrideCursor = null;
        _pressTab = null;
        _dragging = false;
        _dragTab = null;
        _dragSourceBorder = null;
        _dropIndex = -1;
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        while (start is not null)
        {
            if (start is T match)
                return match;
            start = VisualTreeHelper.GetParent(start);
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool>? pred = null) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && (pred is null || pred(t)))
                return t;
            var nested = FindDescendant(child, pred);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private void OnTabCloseClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TermTab tab)
            CloseTab(tab);
        e.Handled = true; // nao propagar pro header (evita re-ativar a aba morta)
    }

    // Sem TerminalTheme o controle não inicializa a paleta e renderiza preto (issue conhecido do wrapper)
    private static TerminalTheme CreateTheme() => new()
    {
        DefaultBackground = EasyTerminalControl.ColorToVal(Color.FromArgb(255, 12, 12, 12)),
        DefaultForeground = EasyTerminalControl.ColorToVal(Colors.LightYellow),
        DefaultSelectionBackground = 0xcccccc,
        CursorStyle = Microsoft.Terminal.Wpf.CursorStyle.BlinkingBar,
        ColorTable = new uint[] { 0x0C0C0C, 0x1F0FC5, 0x0EA113, 0x009CC1, 0xDA3700, 0x981788, 0xDD963A, 0xCCCCCC, 0x767676, 0x5648E7, 0x0CC616, 0xA5F1F9, 0xFF783B, 0x9E00B4, 0xD6D661, 0xF2F2F2 },
    };

    // ===================== Atalhos =====================

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Com Alt pressionado o WPF reporta e.Key = Key.System e a tecla real vem em e.SystemKey
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;

        // Tab/Shift+Tab: WPF ainda pode engolir a tecla (focus nav). Com Win32InputMode=false,
        // \t no ConPTY é o que o PSReadLine espera pra TabCompleteNext / AcceptSuggestion.
        if (key == Key.Tab && (mods == ModifierKeys.None || mods == ModifierKeys.Shift))
        {
            var term = _activeTab?.Control.ConPTYTerm;
            if (term is not null)
            {
                term.WriteToTerm(mods == ModifierKeys.Shift ? "\x1b[Z" : "\t");
                e.Handled = true;
            }
            return;
        }

        if (mods == ModifierKeys.Alt)
        {
            string? target = key switch
            {
                Key.D1 => DesktopPath,
                Key.D2 => ProjectsRoot,
                Key.D3 => NexUnioRoot,
                Key.D4 => NexSalesPath,
                Key.D5 => SfrResgateDigitalPath,
                _ => null,
            };
            if (target is not null)
            {
                QuickCd(target);
                e.Handled = true;
            }
            return;
        }

        if (mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (key)
            {
                case Key.T:
                    NewTab();
                    e.Handled = true;
                    return;
                // Agent-first: abre aba já com o TUI
                case Key.G:
                    NewTab(agent: "grok");
                    e.Handled = true;
                    return;
                case Key.C:
                    NewTab(agent: "claude");
                    e.Handled = true;
                    return;
                case Key.D: // Codex (Ctrl+Shift+X já é limbo)
                    NewTab(agent: "codex");
                    e.Handled = true;
                    return;
                case Key.P:
                    NewTab(agent: "pi");
                    e.Handled = true;
                    return;
                case Key.J:
                    NewTab(agent: "jcode");
                    e.Handled = true;
                    return;
                case Key.W:
                    if (_activeTab is not null)
                        CloseTab(_activeTab);
                    e.Handled = true;
                    return;
                case Key.X:
                    SendActiveTabToLimbo();
                    e.Handled = true;
                    return;
                case Key.L:
                    OpenLimboPopup();
                    e.Handled = true;
                    return;
                case Key.O:
                    OpenProjectPackPopup();
                    e.Handled = true;
                    return;
                case Key.Tab:
                    CycleTab(-1);
                    e.Handled = true;
                    return;
                case Key.Left:
                    if (_activeTab is not null)
                    {
                        var i = _tabs.IndexOf(_activeTab);
                        if (i > 0)
                            MoveTab(_activeTab, i - 1);
                    }
                    e.Handled = true;
                    return;
                case Key.Right:
                    if (_activeTab is not null)
                    {
                        var i = _tabs.IndexOf(_activeTab);
                        if (i >= 0 && i < _tabs.Count - 1)
                            MoveTab(_activeTab, i + 1);
                    }
                    e.Handled = true;
                    return;
            }
        }

        if (mods == ModifierKeys.Control)
        {
            if (key == Key.V && TryPasteClipboardText())
            {
                e.Handled = true;
                return;
            }

            if (key == Key.Tab)
            {
                CycleTab(1);
                e.Handled = true;
                return;
            }
            // Ctrl+1..9: navega pra aba N — se ainda não existir, cria até chegar nela
            var slot = key - Key.D1;
            if (slot >= 0 && slot < 9)
            {
                while (_tabs.Count <= slot)
                    NewTab();
                ActivateTab(_tabs[slot]);
                e.Handled = true;
            }
        }
    }

    private bool TryPasteClipboardText()
    {
        var term = _activeTab?.Control.ConPTYTerm;
        if (term is null)
            return false;

        string text;
        try
        {
            if (!Clipboard.ContainsText(TextDataFormat.UnicodeText))
                return false;

            text = Clipboard.GetText(TextDataFormat.UnicodeText);
        }
        catch (COMException)
        {
            // Clipboard temporarily unavailable: leave Ctrl+V for the terminal app.
            return false;
        }

        // Match terminal paste semantics so multiline text reaches TUIs as one paste
        // instead of executing each newline as an individual Enter key.
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        term.WriteToTerm($"\x1b[200~{text}\x1b[201~");
        return true;
    }

    private void QuickCd(string path)
    {
        // Escreve o comando no PTY como se o usuário tivesse digitado (mesma técnica do TerminalDE)
        _activeTab?.Control.ConPTYTerm?.WriteToTerm($"Set-Location '{path}'\r");
    }

    // ===================== Hotkey global =====================

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        _hotkeyRegistered = RegisterHotKey(_hwnd, HOTKEY_ID_TOGGLE, MOD_CONTROL | MOD_NOREPEAT, VK_SPACE);
        if (!_hotkeyRegistered)
        {
            // Outro app (ex: TerminalDE) segura o Ctrl+Space — tenta de novo a cada 3s (mesma estrategia do TerminalDE)
            _hotkeyRetry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _hotkeyRetry.Tick += (_, _) =>
            {
                _hotkeyRegistered = RegisterHotKey(_hwnd, HOTKEY_ID_TOGGLE, MOD_CONTROL | MOD_NOREPEAT, VK_SPACE);
                if (_hotkeyRegistered)
                    _hotkeyRetry?.Stop();
            };
            _hotkeyRetry.Start();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_TOGGLE)
        {
            ToggleWindow();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ToggleWindow()
    {
        // Esconde só se o KamuiT já está no primeiro plano.
        // Aberto + outro app em foco (ex: Brave) → traz pra frente, não fecha.
        if (IsVisible && WindowState != WindowState.Minimized && IsOurProcessForeground())
        {
            Hide(); // o Windows devolve o foco pra janela anterior automaticamente
            return;
        }

        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Maximized;
        Topmost = true;   // força Z-order acima do app atual
        Activate();
        Topmost = false;
        if (_activeTab is not null)
            Keyboard.Focus(_activeTab.Control);
    }

    /// <summary>
    /// True se a janela em foco pertence a este processo (MainWindow ou HWND do ConPTY).
    /// Comparar por PID é mais confiável que GetAncestor com HwndHost.
    /// </summary>
    private static bool IsOurProcessForeground()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return false;
        GetWindowThreadProcessId(fg, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _hotkeyRetry?.Stop();
        if (_hotkeyRegistered)
            UnregisterHotKey(_hwnd, HOTKEY_ID_TOGGLE);

        try { _commandServer?.Dispose(); } catch { }
        _commandServer = null;

        // Mata todos os pwsh das abas e do limbo
        foreach (var tab in _tabs.Concat(_limbo))
        {
            try
            {
                var proc = tab.Control.ConPTYTerm?.Process;
                if (proc is { HasExited: false })
                    proc.Kill(EntireProcessTree: true);
            }
            catch { }
        }
    }

    // ===================== IPC (CLI / MCP / 2ª instância) =====================

    private KamuiResponse HandleCommand(KamuiRequest req)
    {
        var op = (req.Op ?? "").Trim().ToLowerInvariant();
        return op switch
        {
            "ping" => KamuiResponse.Success("pong"),
            "show" or "summon" or "focus-window" => HandleShow(),
            "list" or "tabs" => HandleList(),
            "open" or "new" or "tab" => HandleOpen(req),
            "focus" or "activate" => HandleFocus(req),
            "type" or "send" or "write" => HandleType(req),
            "close" => HandleClose(req),
            "agents" => new KamuiResponse
            {
                Ok = true,
                Message = string.Join(", ", AgentCatalog.KnownAgents),
            },
            "" => KamuiResponse.Fail("missing op"),
            _ => KamuiResponse.Fail($"unknown op: {op}"),
        };
    }

    private KamuiResponse HandleShow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Maximized;
        Topmost = true;
        Activate();
        Topmost = false;
        if (_activeTab is not null)
            Keyboard.Focus(_activeTab.Control);
        return HandleList();
    }

    private KamuiResponse HandleList()
    {
        return new KamuiResponse
        {
            Ok = true,
            Tabs = _tabs.Select(ToInfo).ToList(),
            Limbo = _limbo.Select(t =>
            {
                var info = ToInfo(t);
                info.Limbo = true;
                info.Slot = 0;
                return info;
            }).ToList(),
        };
    }

    private KamuiTabInfo ToInfo(TermTab t) => new()
    {
        Id = t.Id.ToString(),
        Slot = t.Slot,
        Title = t.Title,
        Agent = t.Agent,
        Cwd = t.WorkingDirectory,
        Active = ReferenceEquals(t, _activeTab),
        Limbo = false,
    };

    private KamuiResponse HandleOpen(KamuiRequest req)
    {
        var count = Math.Clamp(req.Count ?? 1, 1, 9);
        var cwd = string.IsNullOrWhiteSpace(req.Cwd) ? ProjectsRoot : req.Cwd!;
        if (!Directory.Exists(cwd))
            return KamuiResponse.Fail($"cwd not found: {cwd}");

        if (req.Agent is not null && !AgentCatalog.IsKnown(req.Agent))
            return KamuiResponse.Fail($"unknown agent: {req.Agent}. try: grok, claude, codex, pi, jcode, shell");

        var agent = AgentCatalog.Normalize(req.Agent);
        var created = new List<KamuiTabInfo>();
        for (var i = 0; i < count; i++)
        {
            var tab = NewTab(cwd, req.Agent);
            created.Add(ToInfo(tab));
        }

        if (req.Show != false)
            HandleShow();

        return new KamuiResponse
        {
            Ok = true,
            Tabs = created,
            Message = $"opened {count} tab(s)" + (agent is null ? "" : $" with {agent}"),
        };
    }

    private KamuiResponse HandleFocus(KamuiRequest req)
    {
        TermTab? tab = null;
        if (!string.IsNullOrWhiteSpace(req.Id) && Guid.TryParse(req.Id, out var gid))
            tab = _tabs.FirstOrDefault(t => t.Id == gid) ?? _limbo.FirstOrDefault(t => t.Id == gid);
        else if (req.Slot is >= 1)
            tab = _tabs.FirstOrDefault(t => t.Slot == req.Slot);

        if (tab is null)
            return KamuiResponse.Fail("tab not found");

        if (_limbo.Contains(tab))
            RestoreFromLimbo(tab);
        else
            ActivateTab(tab);

        if (req.Show != false)
            HandleShow();

        return new KamuiResponse { Ok = true, Tabs = [ToInfo(tab)] };
    }

    private KamuiResponse HandleType(KamuiRequest req)
    {
        TermTab? tab = _activeTab;
        if (!string.IsNullOrWhiteSpace(req.Id) && Guid.TryParse(req.Id, out var gid))
            tab = _tabs.FirstOrDefault(t => t.Id == gid);
        else if (req.Slot is >= 1)
            tab = _tabs.FirstOrDefault(t => t.Slot == req.Slot);

        if (tab is null)
            return KamuiResponse.Fail("tab not found");

        var text = req.Text ?? "";
        if (req.Enter == true)
            text += "\r";

        try
        {
            tab.Control.ConPTYTerm?.WriteToTerm(text);
        }
        catch (Exception ex)
        {
            return KamuiResponse.Fail(ex.Message);
        }

        return new KamuiResponse { Ok = true, Tabs = [ToInfo(tab)], Message = "typed" };
    }

    private KamuiResponse HandleClose(KamuiRequest req)
    {
        TermTab? tab = null;
        if (!string.IsNullOrWhiteSpace(req.Id) && Guid.TryParse(req.Id, out var gid))
            tab = _tabs.FirstOrDefault(t => t.Id == gid);
        else if (req.Slot is >= 1)
            tab = _tabs.FirstOrDefault(t => t.Slot == req.Slot);
        else
            tab = _activeTab;

        if (tab is null)
            return KamuiResponse.Fail("tab not found");

        CloseTab(tab);
        return HandleList();
    }
}
