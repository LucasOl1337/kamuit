using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
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

    private IntPtr _hwnd;
    private bool _hotkeyRegistered;
    private DispatcherTimer? _hotkeyRetry;

    // --- Tabs ---
    private readonly ObservableCollection<TermTab> _tabs = new();
    private readonly List<TermTab> _limbo = new();
    private TermTab? _activeTab;
    private AgentReadyService? _agentReady;

    // --- Quick CD ---
    private static readonly string DesktopPath =
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    public MainWindow()
    {
        InitializeComponent();
        TabList.ItemsSource = _tabs;

        _agentReady = new AgentReadyService(a => Dispatcher.BeginInvoke(a));
        _agentReady.Start();
        AgentReadyService.InstallHooks();

        SourceInitialized += OnSourceInitialized;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => NewTab();
    }

    // ===================== Tabs =====================

    private TermTab NewTab()
    {
        // Slot 1-based da aba — injetado na sessão pra o hook do agente saber qual som tocar.
        // Sanitiza as vars do TerminalDE: se o KamuiT for lançado de dentro dele (ou de outro
        // terminal contaminado), as abas herdariam TERMINALDE=1 e os agentes disparariam o som dele.
        var slot = _tabs.Count + 1;
        var control = new EasyTerminalControl
        {
            StartupCommandLine = "pwsh.exe -NoLogo -NoExit -Command \"" +
                "$env:TERMINALDE=$null; $env:TERMINALDE_PTY_ID=$null; " +
                "$env:TERMINALDE_SIGNALS_DIR=$null; $env:TERMINALDE_AGENT=$null; " +
                $"$env:KAMUIT='1'; $env:KAMUIT_TAB='{slot}'\"",
            WorkingDirectory = @"C:\projetos",
            Theme = CreateTheme(),
        };
        var tab = new TermTab { Title = "PowerShell", Control = control };

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
        ActivateTab(tab);
        return tab;
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
        _limbo.Add(tab); // o controle continua na TermArea, só Collapsed — PTY vivo, render pausada

        if (_tabs.Count == 0)
            NewTab(); // janela nunca fica sem aba visível
        else
            ActivateTab(_tabs[^1]);
    }

    private void RestoreFromLimbo(TermTab tab)
    {
        _limbo.Remove(tab);
        _tabs.Add(tab);
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

    private void OnTabHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TermTab tab)
            ActivateTab(tab);
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

        if (mods == ModifierKeys.Alt)
        {
            string? target = key switch
            {
                Key.D1 => DesktopPath,
                Key.D2 => @"C:\projetos",
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
                case Key.Tab:
                    CycleTab(-1);
                    e.Handled = true;
                    return;
            }
        }

        if (mods == ModifierKeys.Control)
        {
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
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide(); // o Windows devolve o foco pra janela anterior automaticamente
        }
        else
        {
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Maximized;
            Activate();
            if (_activeTab is not null)
                Keyboard.Focus(_activeTab.Control);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _hotkeyRetry?.Stop();
        if (_hotkeyRegistered)
            UnregisterHotKey(_hwnd, HOTKEY_ID_TOGGLE);

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
}
