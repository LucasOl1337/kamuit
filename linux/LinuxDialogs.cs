namespace KamuiT;

internal static class LinuxDialogs
{
    public static void ShowLimbo(Gtk.Window parent, IReadOnlyList<TermTab> tabs, Action<TermTab> restore)
    {
        var win = Gtk.Window.New();
        win.Title = "Limbo";
        win.SetDefaultSize(420, 280);
        win.TransientFor = parent;
        win.Modal = true;

        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        box.MarginStart = box.MarginEnd = box.MarginTop = box.MarginBottom = 12;
        box.Append(Gtk.Label.New($"{tabs.Count} sessão(ões) em espera"));

        var list = Gtk.ListBox.New();
        list.SelectionMode = Gtk.SelectionMode.Single;
        foreach (var tab in tabs)
        {
            var row = Gtk.Label.New(tab.Title);
            row.Xalign = 0;
            list.Append(row);
        }
        box.Append(list);

        var restoreBtn = Gtk.Button.NewWithLabel("Restaurar");
        restoreBtn.OnClicked += (_, _) =>
        {
            var idx = SelectedIndex(list);
            if (idx >= 0 && idx < tabs.Count)
                restore(tabs[idx]);
            win.Close();
        };
        box.Append(restoreBtn);

        var keys = Gtk.EventControllerKey.New();
        keys.OnKeyPressed += (_, args) =>
        {
            if (args.Keyval is 0xff1b) // Escape
            {
                win.Close();
                return true;
            }
            if (args.Keyval is 0xff0d) // Return
            {
                restoreBtn.Activate();
                return true;
            }
            return false;
        };
        win.AddController(keys);

        win.SetChild(box);
        win.Present();
    }

    public static void ShowProjectPack(Gtk.Window parent, string root, Action<string, int> open)
    {
        var win = Gtk.Window.New();
        win.Title = "Abrir projeto";
        win.SetDefaultSize(480, 360);
        win.TransientFor = parent;
        win.Modal = true;

        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        box.MarginStart = box.MarginEnd = box.MarginTop = box.MarginBottom = 12;
        box.Append(Gtk.Label.New("N abas já no folder — " + root));

        var list = Gtk.ListBox.New();
        list.SelectionMode = Gtk.SelectionMode.Single;
        var dirs = new List<string>();
        try
        {
            if (Directory.Exists(root))
            {
                foreach (var d in Directory.GetDirectories(root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    dirs.Add(d);
                    var row = Gtk.Label.New(Path.GetFileName(d));
                    row.Xalign = 0;
                    list.Append(row);
                }
            }
        }
        catch { }

        var scroll = Gtk.ScrolledWindow.New();
        scroll.Vexpand = true;
        scroll.SetChild(list);
        box.Append(scroll);

        var countBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        countBox.Append(Gtk.Label.New("Abas:"));
        var count = Gtk.SpinButton.NewWithRange(1, 9, 1);
        count.Value = 1;
        countBox.Append(count);
        box.Append(countBox);

        var openBtn = Gtk.Button.NewWithLabel("Abrir");
        openBtn.OnClicked += (_, _) =>
        {
            var idx = SelectedIndex(list);
            if (idx < 0 || idx >= dirs.Count)
                return;
            open(dirs[idx], (int)count.Value);
            win.Close();
        };
        box.Append(openBtn);

        var keys = Gtk.EventControllerKey.New();
        keys.OnKeyPressed += (_, args) =>
        {
            if (args.Keyval is 0xff1b)
            {
                win.Close();
                return true;
            }
            if (args.Keyval is 0xff0d)
            {
                openBtn.Activate();
                return true;
            }
            return false;
        };
        win.AddController(keys);

        win.SetChild(box);
        win.Present();
    }

    private static int SelectedIndex(Gtk.ListBox list)
    {
        var row = list.GetSelectedRow();
        if (row is null)
            return -1;
        var i = 0;
        for (var child = list.GetFirstChild(); child is not null; child = child.GetNextSibling())
        {
            if (ReferenceEquals(child, row))
                return i;
            i++;
        }
        return -1;
    }
}
