using System.Runtime.InteropServices;

namespace KamuiT;

internal sealed class TermTab
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; set; }
    public bool IsActive { get; set; }
    public int Slot { get; set; }
    public string? Agent { get; set; }
    public required string WorkingDirectory { get; init; }
    public nint Vte { get; init; }
    public required Gtk.Label TitleLabel { get; init; }
    public required Gtk.Widget TabLabel { get; init; }
    public int ChildPid { get; set; }
}

internal sealed class Workspace
{
    private const uint GdkTab = 0xff09;
    private const uint GdkLeftTab = 0xfe20;
    private const uint GdkEscape = 0xff1b;

    private readonly Gtk.Application _app;
    private readonly Gtk.ApplicationWindow _window;
    private readonly Gtk.Notebook _notebook;
    private readonly List<TermTab> _tabs = new();
    private readonly List<TermTab> _limbo = new();
    private readonly SynchronizationContext _sync;
    private TermTab? _active;
    private CommandServer? _commandServer;
    private AgentReadyLinux? _ready;

    public Workspace(Gtk.Application app, string[] args)
    {
        _app = app;
        _sync = SynchronizationContext.Current ?? new SynchronizationContext();

        _window = Gtk.ApplicationWindow.New(app);
        _window.Title = "KamuiT";
        _window.SetDefaultSize(1280, 800);
        _window.Maximized = true;

        ApplyCss();

        var header = Gtk.HeaderBar.New();
        header.TitleWidget = Gtk.Label.New("KamuiT");
        var plus = Gtk.Button.NewWithLabel("+");
        plus.TooltipText = "Nova aba (Ctrl+Shift+T)";
        plus.OnClicked += (_, _) => NewTab();
        header.PackEnd(plus);
        _window.SetTitlebar(header);

        _notebook = Gtk.Notebook.New();
        _notebook.Scrollable = true;
        _notebook.OnSwitchPage += (_, _) => SyncActiveFromNotebook();
        _window.SetChild(_notebook);

        var keys = Gtk.EventControllerKey.New();
        keys.OnKeyPressed += OnKeyPressed; // returns bool: handled
        _window.AddController(keys);

        _window.OnCloseRequest += (_, _) =>
        {
            ShutdownSessions();
            return false;
        };

        _ready = new AgentReadyLinux(a => _sync.Post(_ => a(), null), ResolveReadySlot);
        _ready.Start();

        _commandServer = new CommandServer(
            f =>
            {
                var tcs = new TaskCompletionSource<KamuiResponse>();
                _sync.Post(_ =>
                {
                    try { tcs.SetResult(f()); }
                    catch (Exception ex) { tcs.SetResult(KamuiResponse.Fail(ex.Message)); }
                }, null);
                return tcs.Task;
            },
            HandleCommand);
        _commandServer.Start();

        if (args.Length > 0)
        {
            var req = KamuiRequest.ParseCli(args);
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

    public void Present()
    {
        _window.Present();
        _window.Maximize();
    }

    private void ApplyCss()
    {
        try
        {
            var css = Gtk.CssProvider.New();
            css.LoadFromString("""
                window { background: #0c0c0c; }
                headerbar { background: #181818; color: #cccccc; }
                notebook { background: #0c0c0c; }
                notebook > header { background: #181818; }
                notebook > header tabs tab { padding: 6px 10px; }
                """);
            var display = Gdk.Display.GetDefault();
            if (display is not null)
                Gtk.StyleContext.AddProviderForDisplay(display, css, 600);
        }
        catch { /* tema default se CSS falhar */ }
    }

    private TermTab NewTab(string? workingDirectory = null, string? agent = null)
    {
        var id = Guid.NewGuid();
        var slot = _tabs.Count + 1;
        var cwd = string.IsNullOrWhiteSpace(workingDirectory) ? LinuxPaths.ProjectsRoot : workingDirectory;
        if (!Directory.Exists(cwd))
            cwd = LinuxPaths.ProjectsRoot;

        var agentId = AgentCatalog.Normalize(agent);
        var launch = AgentCatalog.BuildUnixLaunchCommand(agentId);
        var argv = LinuxPaths.BuildShellArgv(launch);

        var vte = VteNative.NewTerminal();
        var titleLabel = Gtk.Label.New("");
        titleLabel.Ellipsize = Pango.EllipsizeMode.End;
        titleLabel.MaxWidthChars = 28;

        var tabBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
        var close = Gtk.Button.NewFromIconName("window-close-symbolic");
        close.HasFrame = false;
        tabBox.Append(titleLabel);
        tabBox.Append(close);

        var folderName = Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar)) ?? "shell";
        var baseTitle = string.IsNullOrEmpty(folderName) ? "shell" : folderName;
        var title = agentId is null ? baseTitle : $"{agentId} · {baseTitle}";
        titleLabel.SetText(title);

        var tab = new TermTab
        {
            Id = id,
            Title = title,
            Vte = vte,
            TitleLabel = titleLabel,
            TabLabel = tabBox,
            Slot = slot,
            Agent = agentId,
            WorkingDirectory = cwd,
        };
        close.OnClicked += (_, _) => CloseTab(tab);

        VteNative.VoidSignal onTitle = (instance, _) =>
        {
            var t = VteNative.GetWindowTitle(instance);
            if (string.IsNullOrWhiteSpace(t))
                return;
            _sync.Post(_ =>
            {
                tab.Title = t;
                tab.TitleLabel.SetText(t);
            }, null);
        };
        VteNative.Connect(vte, "window-title-changed", onTitle);

        var env = new Dictionary<string, string>
        {
            ["KAMUIT"] = "1",
            ["KAMUIT_TAB_ID"] = id.ToString(),
            ["KAMUIT_TAB"] = slot.ToString(),
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor",
            ["TERM_PROGRAM"] = "KamuiT",
            ["TERM_PROGRAM_VERSION"] = "0.3.0",
        };
        if (agentId is not null)
            env["KAMUIT_AGENT"] = agentId;

        VteNative.Spawn(vte, cwd, argv, env, pid => tab.ChildPid = pid);

        var nb = VteNative.HandleOf(_notebook);
        var labelHandle = VteNative.HandleOf(tabBox);
        VteNative.AppendPage(nb, vte, labelHandle);
        _tabs.Add(tab);
        RefreshSlots();
        ActivateTab(tab);
        return tab;
    }

    private void RefreshSlots()
    {
        for (var i = 0; i < _tabs.Count; i++)
            _tabs[i].Slot = i + 1;
    }

    private void ActivateTab(TermTab tab)
    {
        if (_active is not null)
            _active.IsActive = false;
        _active = tab;
        tab.IsActive = true;
        var nb = VteNative.HandleOf(_notebook);
        var page = VteNative.PageNum(nb, tab.Vte);
        if (page >= 0)
            VteNative.SetCurrentPage(nb, page);
        VteNative.GrabFocus(tab.Vte);
    }

    private void SyncActiveFromNotebook()
    {
        var nb = VteNative.HandleOf(_notebook);
        var n = VteNative.NPages(nb);
        // current page is set after switch; match by visible page child
        for (var i = 0; i < n; i++)
        {
            var child = VteNative.NthPage(nb, i);
            var tab = _tabs.FirstOrDefault(t => t.Vte == child);
            if (tab is null)
                continue;
            // Notebook current page: if this child is the selected one, Activate without recursion
        }

        try
        {
            var current = _notebook.GetCurrentPage();
            var child = VteNative.NthPage(nb, current);
            var tab = _tabs.FirstOrDefault(t => t.Vte == child);
            if (tab is not null && !ReferenceEquals(tab, _active))
            {
                if (_active is not null)
                    _active.IsActive = false;
                _active = tab;
                tab.IsActive = true;
            }
        }
        catch { }
    }

    private void CloseTab(TermTab tab)
    {
        try
        {
            if (tab.ChildPid > 0)
                VteNative.KillPid(tab.ChildPid, 15);
        }
        catch { }

        var nb = VteNative.HandleOf(_notebook);
        var page = VteNative.PageNum(nb, tab.Vte);
        if (page >= 0)
            VteNative.RemovePage(nb, page);
        else
            VteNative.Unparent(tab.Vte);

        VteNative.Unref(tab.Vte);
        _tabs.Remove(tab);
        _limbo.Remove(tab);
        RefreshSlots();

        if (_tabs.Count == 0)
        {
            _window.Close();
            return;
        }
        if (ReferenceEquals(_active, tab))
            ActivateTab(_tabs[Math.Min(page < 0 ? 0 : page, _tabs.Count - 1)]);
    }

    private void CycleTab(int direction)
    {
        if (_active is null || _tabs.Count < 2)
            return;
        var index = _tabs.IndexOf(_active);
        var next = (index + direction + _tabs.Count) % _tabs.Count;
        ActivateTab(_tabs[next]);
    }

    private void SendActiveToLimbo()
    {
        if (_active is null)
            return;
        var tab = _active;
        var nb = VteNative.HandleOf(_notebook);
        VteNative.DetachTab(nb, tab.Vte);
        _tabs.Remove(tab);
        tab.Slot = 0;
        _limbo.Add(tab);
        RefreshSlots();
        if (_tabs.Count == 0)
            NewTab();
        else
            ActivateTab(_tabs[^1]);
    }

    private void RestoreFromLimbo(TermTab tab)
    {
        _limbo.Remove(tab);
        var nb = VteNative.HandleOf(_notebook);
        var labelHandle = VteNative.HandleOf(tab.TabLabel);
        VteNative.AppendPage(nb, tab.Vte, labelHandle);
        _tabs.Add(tab);
        RefreshSlots();
        ActivateTab(tab);
    }

    private void OpenLimboPopup()
    {
        if (_limbo.Count == 0)
            return;
        LinuxDialogs.ShowLimbo(_window, _limbo, RestoreFromLimbo);
    }

    private void OpenProjectPackPopup()
    {
        LinuxDialogs.ShowProjectPack(_window, LinuxPaths.ProjectsRoot, (path, count) =>
        {
            count = Math.Clamp(count, 1, 9);
            for (var i = 0; i < count; i++)
                NewTab(path);
        });
    }

    private void QuickCd(string path)
    {
        if (_active is null)
            return;
        var quoted = "'" + path.Replace("'", "'\\''") + "'";
        VteNative.FeedChild(_active.Vte, "cd " + quoted + "\n");
    }

    private int? ResolveReadySlot(string? tabId, int? legacyTab)
    {
        if (!string.IsNullOrWhiteSpace(tabId) && Guid.TryParse(tabId, out var id))
        {
            for (var i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id == id)
                    return i + 1;
            }
            return null;
        }
        if (legacyTab is >= 1 and <= 5)
            return legacyTab;
        return null;
    }

    private bool OnKeyPressed(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyPressedSignalArgs args)
    {
        var key = args.Keyval;
        var state = args.State;
        var ctrl = state.HasFlag(Gdk.ModifierType.ControlMask);
        var shift = state.HasFlag(Gdk.ModifierType.ShiftMask);
        var alt = state.HasFlag(Gdk.ModifierType.AltMask);

        if (alt && !ctrl)
        {
            string? target = key switch
            {
                (uint)'1' or 0x0031 => LinuxPaths.Desktop,
                (uint)'2' or 0x0032 => LinuxPaths.ProjectsRoot,
                (uint)'3' or 0x0033 => LinuxPaths.NexUnioRoot,
                (uint)'4' or 0x0034 => LinuxPaths.NexSalesPath,
                (uint)'5' or 0x0035 => LinuxPaths.SfrResgateDigitalPath,
                _ => null,
            };
            if (target is not null)
            {
                QuickCd(target);
                return true;
            }
            return false;
        }

        if (ctrl && shift)
        {
            var handled = true;
            switch (key)
            {
                case (uint)'t' or (uint)'T':
                    NewTab();
                    break;
                case (uint)'g' or (uint)'G':
                    NewTab(agent: "grok");
                    break;
                case (uint)'c' or (uint)'C':
                    NewTab(agent: "claude");
                    break;
                case (uint)'d' or (uint)'D':
                    NewTab(agent: "codex");
                    break;
                case (uint)'p' or (uint)'P':
                    NewTab(agent: "pi");
                    break;
                case (uint)'j' or (uint)'J':
                    NewTab(agent: "jcode");
                    break;
                case (uint)'w' or (uint)'W':
                    if (_active is not null)
                        CloseTab(_active);
                    break;
                case (uint)'x' or (uint)'X':
                    SendActiveToLimbo();
                    break;
                case (uint)'l' or (uint)'L':
                    OpenLimboPopup();
                    break;
                case (uint)'o' or (uint)'O':
                    OpenProjectPackPopup();
                    break;
                case GdkTab or GdkLeftTab:
                    CycleTab(-1);
                    break;
                default:
                    handled = false;
                    break;
            }
            return handled;
        }

        if (ctrl && !shift)
        {
            if (key is GdkTab)
            {
                CycleTab(1);
                return true;
            }
            if (key is >= (uint)'1' and <= (uint)'9')
            {
                var slot = (int)(key - '1');
                while (_tabs.Count <= slot)
                    NewTab();
                ActivateTab(_tabs[slot]);
                return true;
            }
        }

        return false;
    }

    private void ShutdownSessions()
    {
        try { _commandServer?.Dispose(); } catch { }
        _commandServer = null;
        foreach (var tab in _tabs.Concat(_limbo))
        {
            try
            {
                if (tab.ChildPid > 0)
                    VteNative.KillPid(tab.ChildPid, 15);
            }
            catch { }
        }
    }

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
        Present();
        if (_active is not null)
            VteNative.GrabFocus(_active.Vte);
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
        Active = ReferenceEquals(t, _active),
        Limbo = false,
    };

    private KamuiResponse HandleOpen(KamuiRequest req)
    {
        var count = Math.Clamp(req.Count ?? 1, 1, 9);
        var cwd = string.IsNullOrWhiteSpace(req.Cwd) ? LinuxPaths.ProjectsRoot : req.Cwd!;
        if (!Directory.Exists(cwd))
            return KamuiResponse.Fail($"cwd not found: {cwd}");

        if (req.Agent is not null && !AgentCatalog.IsKnown(req.Agent))
            return KamuiResponse.Fail($"unknown agent: {req.Agent}. try: grok, claude, codex, pi, jcode, shell");

        var agent = AgentCatalog.Normalize(req.Agent);
        var created = new List<KamuiTabInfo>();
        for (var i = 0; i < count; i++)
            created.Add(ToInfo(NewTab(cwd, req.Agent)));

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
        TermTab? tab = _active;
        if (!string.IsNullOrWhiteSpace(req.Id) && Guid.TryParse(req.Id, out var gid))
            tab = _tabs.FirstOrDefault(t => t.Id == gid);
        else if (req.Slot is >= 1)
            tab = _tabs.FirstOrDefault(t => t.Slot == req.Slot);

        if (tab is null)
            return KamuiResponse.Fail("tab not found");

        var text = req.Text ?? "";
        if (req.Enter == true)
            text += "\n";

        try
        {
            VteNative.FeedChild(tab.Vte, text);
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
            tab = _active;

        if (tab is null)
            return KamuiResponse.Fail("tab not found");

        CloseTab(tab);
        return HandleList();
    }
}
