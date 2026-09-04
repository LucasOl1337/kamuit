using System.Runtime.InteropServices;

namespace KamuiT;

/// <summary>
/// P/Invoke de libvte-2.91-gtk4 (widget nativo de terminal no Linux).
/// Equivalente funcional do Windows Terminal core no host WPF.
/// </summary>
internal static class VteNative
{
    private static nint _vte;
    private static nint _gtk;
    private static nint _gobject;
    private static nint _pango;
    private static nint _glib;

    private static NewTermFn _newTerm = null!;
    private static SpawnAsyncFn _spawnAsync = null!;
    private static FeedChildFn _feedChild = null!;
    private static GetTitleFn _getTitle = null!;
    private static SetColorFn _setColorBg = null!;
    private static SetColorFn _setColorFg = null!;
    private static SetFontFn _setFont = null!;
    private static SetScrollbackFn _setScrollback = null!;
    private static SetBoolFn _setHexpand = null!;
    private static SetBoolFn _setVexpand = null!;
    private static NotebookAppendFn _notebookAppend = null!;
    private static NotebookDetachFn _notebookDetach = null!;
    private static NotebookRemoveFn _notebookRemove = null!;
    private static NotebookPageNumFn _notebookPageNum = null!;
    private static NotebookNthPageFn _notebookNthPage = null!;
    private static NotebookSetPageFn _notebookSetPage = null!;
    private static NotebookNPagesFn _notebookNPages = null!;
    private static GSignalConnectFn _signalConnect = null!;
    private static GObjectRefFn _refSink = null!;
    private static GObjectRefFn _unref = null!;
    private static FontFromStringFn _fontFromString = null!;
    private static FontFreeFn _fontFree = null!;
    private static WidgetGrabFocusFn _grabFocus = null!;
    private static WidgetUnparentFn _unparent = null!;
    private static KillFn _kill = null!;

    private const int GSpawnSearchPath = 4;
    private const int PtyDefault = 0;

    public static string LoadedLibrary { get; private set; } = "";

    public static void Load()
    {
        if (_vte != nint.Zero)
            return;

        _gtk = LoadOne("libgtk-4.so.1", "libgtk-4.so");
        _gobject = LoadOne("libgobject-2.0.so.0", "libgobject-2.0.so");
        _glib = LoadOne("libglib-2.0.so.0", "libglib-2.0.so");
        _pango = LoadOne("libpango-1.0.so.0", "libpango-1.0.so");
        _vte = LoadOne(
            "libvte-2.91-gtk4.so.0",
            "libvte-2.91-gtk4.so",
            "libvte-2.91.so.0",
            "libvte-2.91.so");

        _newTerm = Get<NewTermFn>(_vte, "vte_terminal_new");
        _spawnAsync = Get<SpawnAsyncFn>(_vte, "vte_terminal_spawn_async");
        _feedChild = Get<FeedChildFn>(_vte, "vte_terminal_feed_child");
        _getTitle = Get<GetTitleFn>(_vte, "vte_terminal_get_window_title");
        _setColorBg = Get<SetColorFn>(_vte, "vte_terminal_set_color_background");
        _setColorFg = Get<SetColorFn>(_vte, "vte_terminal_set_color_foreground");
        _setFont = Get<SetFontFn>(_vte, "vte_terminal_set_font");
        _setScrollback = Get<SetScrollbackFn>(_vte, "vte_terminal_set_scrollback_lines");
        _setHexpand = Get<SetBoolFn>(_gtk, "gtk_widget_set_hexpand");
        _setVexpand = Get<SetBoolFn>(_gtk, "gtk_widget_set_vexpand");
        _notebookAppend = Get<NotebookAppendFn>(_gtk, "gtk_notebook_append_page");
        _notebookDetach = Get<NotebookDetachFn>(_gtk, "gtk_notebook_detach_tab");
        _notebookRemove = Get<NotebookRemoveFn>(_gtk, "gtk_notebook_remove_page");
        _notebookPageNum = Get<NotebookPageNumFn>(_gtk, "gtk_notebook_page_num");
        _notebookNthPage = Get<NotebookNthPageFn>(_gtk, "gtk_notebook_get_nth_page");
        _notebookSetPage = Get<NotebookSetPageFn>(_gtk, "gtk_notebook_set_current_page");
        _notebookNPages = Get<NotebookNPagesFn>(_gtk, "gtk_notebook_get_n_pages");
        _signalConnect = Get<GSignalConnectFn>(_gobject, "g_signal_connect_data");
        _refSink = Get<GObjectRefFn>(_gobject, "g_object_ref_sink");
        _unref = Get<GObjectRefFn>(_gobject, "g_object_unref");
        _fontFromString = Get<FontFromStringFn>(_pango, "pango_font_description_from_string");
        _fontFree = Get<FontFreeFn>(_pango, "pango_font_description_free");
        _grabFocus = Get<WidgetGrabFocusFn>(_gtk, "gtk_widget_grab_focus");
        _unparent = Get<WidgetUnparentFn>(_gtk, "gtk_widget_unparent");
        _kill = Get<KillFn>(NativeLibrary.Load("libc.so.6"), "kill");
    }

    public static nint NewTerminal()
    {
        var handle = _newTerm();
        if (handle == nint.Zero)
            throw new InvalidOperationException("vte_terminal_new returned null");
        _refSink(handle);
        _setHexpand(handle, true);
        _setVexpand(handle, true);
        _setScrollback(handle, 10000);

        var bg = new GdkRGBA { Red = 12 / 255f, Green = 12 / 255f, Blue = 12 / 255f, Alpha = 1 };
        var fg = new GdkRGBA { Red = 1, Green = 1, Blue = 224 / 255f, Alpha = 1 };
        _setColorBg(handle, ref bg);
        _setColorFg(handle, ref fg);

        var font = _fontFromString("Monospace 11");
        if (font != nint.Zero)
        {
            _setFont(handle, font);
            _fontFree(font);
        }
        return handle;
    }

    public static void Spawn(
        nint terminal,
        string workingDirectory,
        string[] argv,
        IDictionary<string, string> extraEnv,
        Action<int>? onPid = null)
    {
        var envList = new List<string>();
        foreach (System.Collections.DictionaryEntry kv in Environment.GetEnvironmentVariables())
        {
            var key = kv.Key?.ToString();
            if (string.IsNullOrEmpty(key))
                continue;
            envList.Add(key + "=" + (kv.Value?.ToString() ?? ""));
        }
        foreach (var (k, v) in extraEnv)
        {
            envList.RemoveAll(e => e.StartsWith(k + "=", StringComparison.Ordinal));
            envList.Add(k + "=" + v);
        }

        SpawnCb cb = (_, pid, error, _) =>
        {
            if (error != nint.Zero)
                return;
            onPid?.Invoke(pid);
        };
        GCHandle.Alloc(cb);

        var argvPtr = AllocStringArray(argv);
        var envPtr = AllocStringArray(envList);
        var cwdPtr = Marshal.StringToCoTaskMemUTF8(workingDirectory);
        try
        {
            _spawnAsync(
                terminal,
                PtyDefault,
                cwdPtr,
                argvPtr,
                envPtr,
                GSpawnSearchPath,
                nint.Zero, nint.Zero, nint.Zero,
                -1,
                nint.Zero,
                Marshal.GetFunctionPointerForDelegate(cb),
                nint.Zero);
        }
        finally
        {
            FreeStringArray(argvPtr, argv.Length);
            FreeStringArray(envPtr, envList.Count);
            Marshal.FreeCoTaskMem(cwdPtr);
        }
    }

    public static void FeedChild(nint terminal, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        _feedChild(terminal, bytes, bytes.Length);
    }

    public static string? GetWindowTitle(nint terminal)
    {
        var p = _getTitle(terminal);
        return p == nint.Zero ? null : Marshal.PtrToStringUTF8(p);
    }

    public static ulong Connect(nint instance, string signal, Delegate handler)
    {
        var gch = GCHandle.Alloc(handler);
        var fn = Marshal.GetFunctionPointerForDelegate(handler);
        var name = Marshal.StringToCoTaskMemUTF8(signal);
        try
        {
            return _signalConnect(instance, name, fn, GCHandle.ToIntPtr(gch), nint.Zero, 0);
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    public static int AppendPage(nint notebook, nint child, nint tabLabel) =>
        _notebookAppend(notebook, child, tabLabel);

    public static void DetachTab(nint notebook, nint child) => _notebookDetach(notebook, child);

    public static void RemovePage(nint notebook, int page) => _notebookRemove(notebook, page);

    public static int PageNum(nint notebook, nint child) => _notebookPageNum(notebook, child);

    public static nint NthPage(nint notebook, int page) => _notebookNthPage(notebook, page);

    public static void SetCurrentPage(nint notebook, int page) => _notebookSetPage(notebook, page);

    public static int NPages(nint notebook) => _notebookNPages(notebook);

    public static void GrabFocus(nint widget) => _grabFocus(widget);

    public static void Unparent(nint widget) => _unparent(widget);

    public static void Unref(nint obj)
    {
        if (obj != nint.Zero)
            _unref(obj);
    }

    public static void KillPid(int pid, int sig = 15)
    {
        if (pid > 0)
            _kill(pid, sig);
    }

    public static nint HandleOf(GObject.Object obj) => obj.Handle.DangerousGetHandle();

    private static nint LoadOne(params string[] names)
    {
        foreach (var n in names)
        {
            if (NativeLibrary.TryLoad(n, out var h))
            {
                if (n.Contains("vte", StringComparison.Ordinal))
                    LoadedLibrary = n;
                return h;
            }
        }
        throw new DllNotFoundException(
            "Biblioteca nativa ausente. Tentou: " + string.Join(", ", names)
            + ". Instale GTK4 + VTE (ver scripts/install-linux.sh).");
    }

    private static T Get<T>(nint lib, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, name));

    private static nint AllocStringArray(IReadOnlyList<string> items)
    {
        var array = Marshal.AllocHGlobal(nint.Size * (items.Count + 1));
        for (var i = 0; i < items.Count; i++)
            Marshal.WriteIntPtr(array, i * nint.Size, Marshal.StringToCoTaskMemUTF8(items[i]));
        Marshal.WriteIntPtr(array, items.Count * nint.Size, nint.Zero);
        return array;
    }

    private static void FreeStringArray(nint array, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var p = Marshal.ReadIntPtr(array, i * nint.Size);
            if (p != nint.Zero)
                Marshal.FreeCoTaskMem(p);
        }
        Marshal.FreeHGlobal(array);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GdkRGBA
    {
        public float Red, Green, Blue, Alpha;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint NewTermFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SpawnAsyncFn(
        nint terminal, int ptyFlags, nint workingDirectory,
        nint argv, nint envv, int spawnFlags,
        nint childSetup, nint childSetupData, nint childSetupDestroy,
        int timeout, nint cancellable, nint callback, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FeedChildFn(nint terminal, byte[] data, nint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetTitleFn(nint terminal);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetColorFn(nint terminal, ref GdkRGBA color);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetFontFn(nint terminal, nint fontDesc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetScrollbackFn(nint terminal, nint lines);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetBoolFn(nint widget, [MarshalAs(UnmanagedType.I1)] bool value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NotebookAppendFn(nint notebook, nint child, nint tabLabel);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotebookDetachFn(nint notebook, nint child);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotebookRemoveFn(nint notebook, int pageNum);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NotebookPageNumFn(nint notebook, nint child);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint NotebookNthPageFn(nint notebook, int pageNum);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotebookSetPageFn(nint notebook, int pageNum);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NotebookNPagesFn(nint notebook);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong GSignalConnectFn(nint instance, nint signal, nint handler, nint data, nint destroy, int flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GObjectRefFn(nint obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint FontFromStringFn(string desc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FontFreeFn(nint desc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void WidgetGrabFocusFn(nint widget);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void WidgetUnparentFn(nint widget);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int KillFn(int pid, int sig);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void VoidSignal(nint instance, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SpawnCb(nint terminal, int pid, nint error, nint userData);
}
