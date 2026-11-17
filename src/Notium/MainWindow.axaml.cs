// ---- MainWindow.axaml.cs
namespace Notium
{
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

public partial class MainWindow : Window
{
    readonly List<TabDoc> _tabs = new();
    readonly Dictionary<TabDoc, Border> _views = new();
    readonly Stack<TabDoc> _closed = new();
    TabDoc _cur, _hover;
    bool _allowClose, _checkingExternal, _zen;
    WindowState _stateBeforeZen = WindowState.Normal;
    Highlighter _hl;
    readonly Dictionary<string, Action> _cmd;
    Window _filesWin;

    public MainWindow()
    {
        InitializeComponent();
        App.Windows.Add(this);
        _cmd = new Dictionary<string, Action>
        {
            ["new-tab"] = NewTab, ["new-tab-alt"] = NewTab,
            ["new-window"] = () => new MainWindow().Show(),
            ["open"] = () => _ = OpenDialogAsync(),
            ["save"] = () => { if (_cur != null) _ = SaveTab(_cur); },
            ["save-as"] = () => { if (_cur != null) _ = SaveAs(_cur); },
            ["close-tab"] = () => { if (_cur != null) _ = CloseTab(_cur); },
            ["reopen-tab"] = ReopenClosed,
            ["bookmark"] = ToggleBookmark, ["bookmark-next"] = () => JumpBookmark(1), ["bookmark-prev"] = () => JumpBookmark(-1),
            ["pin-tab"] = () => { if (_cur != null) TogglePin(_cur); },
            ["print"] = PrintCurrent,
            ["next-tab"] = () => SelectOffset(1), ["prev-tab"] = () => SelectOffset(-1),
            ["find"] = () => ShowFind(false), ["replace"] = () => ShowFind(true),
            ["find-next"] = () => FindStep(true), ["find-prev"] = () => FindStep(false),
            ["find-in-files"] = ShowFindInFiles,
            ["goto-line"] = () => _ = GoToLineAsync(),
            ["move-up"] = () => MoveLines(-1), ["move-down"] = () => MoveLines(1),
            ["comment"] = ToggleComment,
            ["delete-line"] = DeleteLine, ["duplicate-line"] = DuplicateLine,
            ["insert-datetime"] = InsertDateTime,
            ["sidebar"] = ToggleSidebar,
            ["zoom-in"] = () => Zoom(1), ["zoom-out"] = () => Zoom(-1), ["zoom-reset"] = ResetZoom,
            ["zen"] = ToggleZen,
            ["settings"] = () => SettingsWindow.ShowSingle(this),
        };
        _hl = new Highlighter(Editor);
        Map.Attach(Editor);
        SidePanel.Child = BuildSidebar();
        // the mouse wheel over the tabs scrolls them sideways
        TabScroll.AddHandler(PointerWheelChangedEvent, (tabSender, wheel) =>
        {
            double d = Math.Abs(wheel.Delta.Y) > Math.Abs(wheel.Delta.X) ? wheel.Delta.Y : wheel.Delta.X;
            TabScroll.Offset = new Vector(Math.Max(0, TabScroll.Offset.X - d * 60), 0);
            wheel.Handled = true;
        }, RoutingStrategies.Tunnel);
        Editor.AddHandler(PointerWheelChangedEvent, OnEditorWheel, RoutingStrategies.Tunnel);
        Editor.TextArea.Caret.PositionChanged += (s, e) => UpdateStatus();
        Editor.TextArea.SelectionChanged += (s, e) => UpdateStatus();
        Editor.TextArea.TextView.BackgroundRenderers.Add(new BookmarkRenderer(() => _cur));
        AppSettings.Changed += OnSettingsChanged;
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
        Activated += (s, e) => _ = CheckExternalChangesAsync();
        Closing += OnClosing;
        Fx.SoftDrag(this);
        var ws = AppSettings.Current;
        if (ws.RememberWindowSize && ws.WinWidth >= 480 && ws.WinHeight >= 300)
        {
            Width = ws.WinWidth; Height = ws.WinHeight;                                  // the size chosen by the user last time
            if (ws.WinMaximized && !App.Windows.Any(w => w != this)) WindowState = WindowState.Maximized;
        }
        if (OperatingSystem.IsWindows())
        {
            // one single bar: the tabs sit in the title bar (the system buttons stay on the right)
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;     // we draw the window buttons ourselves
            ExtendClientAreaTitleBarHeightHint = -1;
            CaptionBtns.IsVisible = true;
            PropertyChanged += (s, e) =>
            {
                if (e.Property == OffScreenMarginProperty) Root.Margin = OffScreenMargin;
                if (e.Property == WindowStateProperty)         // the maximize button shows one square, or two when maximized
                    CapMaxIcon.Data = Geometry.Parse(WindowState == WindowState.Maximized
                        ? "M2.5,2.5 L2.5,0.5 L9.5,0.5 L9.5,7.5 L7.5,7.5 M0.5,2.5 L7.5,2.5 L7.5,9.5 L0.5,9.5 Z"
                        : "M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z");
            };
            TabBar.PointerPressed += TitleBar_PointerPressed;
        }
        Closed += (s, e) =>
        {
            AppSettings.Changed -= OnSettingsChanged;
            if (AppSettings.Current.RememberWindowSize)
            {
                var st = AppSettings.Current;
                if (WindowState == WindowState.Maximized) st.WinMaximized = true;
                else if (WindowState == WindowState.Normal) { st.WinMaximized = false; st.WinWidth = (int)Math.Round(ClientSize.Width); st.WinHeight = (int)Math.Round(ClientSize.Height); }
            }
            RememberPos(_cur);
            foreach (var pt in _tabs) StorePos(pt);
            AppSettings.Save();
            App.Windows.Remove(this);
            App.CloseIfLast();
        };
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (s, e) => { e.DragEffects = DragDropEffects.Copy; });
        AddHandler(DragDrop.DropEvent, OnDropFiles);
        WireFind();
        ApplySettings();
    }

    // ------------------------------------------------------------ start-up
    /// <summary>Reopens the previous tabs when "restore tabs" is on (default); otherwise starts from an empty document. Files given on the command line are always opened.</summary>
    public void StartUp(string[] args, bool dataFolderProblem)
    {
        if (AppSettings.Current.RestoreSession)
        {
            var restored = Session.Load();
            foreach (var t in restored) AddTab(t, false);
            if (restored.Count > 0) SelectTab(restored[0]);
        }
        if (args != null) foreach (var a in args) if (File.Exists(a)) OpenPath(a);
        if (_tabs.Count == 0) NewTab();
        if (dataFolderProblem)
            _ = MessageDialog.Error(this, "The data folder is not writable:\n" + AppSettings.DataDir + "\n\nSettings will not be saved.");
    }

    // ------------------------------------------------------------ closing
    void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;                 // ask first, then close for real
        _ = RequestCloseAsync();
    }

    /// <summary>Asks to save every modified tab, then closes this window. Returns false if the user cancelled.</summary>
    public async Task<bool> RequestCloseAsync()
    {
        if (AppSettings.Current.RestoreSession && App.Windows.Count == 1)
        {
            Session.Save(_tabs);             // the next start brings these tabs back
            _allowClose = true;
            Close();
            return true;
        }
        foreach (var t in _tabs.ToList())
            if (!await ConfirmSave(t)) return false;
        _allowClose = true;
        Close();
        return true;
    }

    /// <summary>Quits the program: closes every window, asking to save where needed.</summary>
    public static async Task ExitAllAsync()
    {
        if (AppSettings.Current.RestoreSession)
        {
            Session.Save(App.Windows.SelectMany(w => w._tabs));
            foreach (var w in App.Windows.ToList()) { w._allowClose = true; w.Close(); }
            return;
        }
        foreach (var w in App.Windows.ToList())
            if (!await w.RequestCloseAsync()) return;
    }

    /// <summary>Notepad-style prompt. Returns true if it is fine to continue (saved or discarded).</summary>
    async Task<bool> ConfirmSave(TabDoc t)
    {
        if (!t.NeedsSave) return true;
        SelectTab(t);
        while (true)
        {
            int r = await MessageDialog.Show(this, $"Do you want to save changes to {t.Title}?", "Save", "Don't save", "Cancel");
            if (r == 0) return await SaveTab(t);
            if (r == 1)
            {
                // a second question, so that nothing is thrown away by mistake
                int c = await MessageDialog.Show(this, $"Are you sure you do not want to save \"{t.Title}\"?\nYour changes will be lost.", "Don't save", "Go back");
                if (c == 0) return true;
                continue;                       // back to the first question
            }
            return false;                       // Cancel (or the dialog was closed)
        }
    }

    // ------------------------------------------------------------ tabs
    public void NewTab()
    {
        var t = new TabDoc();
        // the lowest number not in use: closing "Untitled 3" and opening a new tab gives "Untitled 3" again
        var used = new HashSet<string>(App.Windows.SelectMany(w => w._tabs).Select(x => x.Title));
        int num = 1;
        while (used.Contains(num == 1 ? "Untitled" : "Untitled " + num)) num++;
        t.Title = num == 1 ? "Untitled" : "Untitled " + num;
        t.Dirty = false;
        AddTab(t, true);
    }

    public void AddTab(TabDoc t, bool select, int index = -1)
    {
        if (index < 0 || index > _tabs.Count) index = _tabs.Count;
        _tabs.Insert(index, t);
        t.Changed += OnTabChanged;
        var v = BuildTabView(t);
        _views[t] = v;
        TabPanel.Children.Insert(index, v);
        if (select) SelectTab(t); else RefreshTabs();
    }

    public void AdoptTab(TabDoc t) { AddTab(t, true); Activate(); }

    public bool HasTabs => _tabs.Count > 0;

    /// <summary>The tab of this window that shows the given file, or null.</summary>
    public TabDoc FindTab(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); } catch { full = path; }
        var cmp = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return _tabs.FirstOrDefault(t => t.Path != null && string.Equals(SafeFull(t.Path), full, cmp));
    }

    static string SafeFull(string p) { try { return Path.GetFullPath(p); } catch { return p; } }

    /// <summary>Selects the tab and brings this window in front of everything (restoring it if minimized).</summary>
    public void ShowTab(TabDoc t)
    {
        if (t != null && _tabs.Contains(t)) SelectTab(t);
        BringToFront();
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        // the system sometimes only blinks the taskbar button: a short "topmost" moment forces the window to the front
        Topmost = true;
        Avalonia.Threading.DispatcherTimer.RunOnce(() => { Topmost = false; Editor.Focus(); }, TimeSpan.FromMilliseconds(150));
    }

    void RemoveTab(TabDoc t)
    {
        int i = _tabs.IndexOf(t);
        if (i < 0) return;
        if (_cur == t) RememberPos(t);
        StorePos(t); AppSettings.Save();
        t.Changed -= OnTabChanged;
        _tabs.RemoveAt(i);
        TabPanel.Children.Remove(_views[t]); _views.Remove(t);
        if (_cur == t)
        {
            _cur = null;
            if (_tabs.Count > 0) SelectTab(_tabs[Math.Min(i, _tabs.Count - 1)]);
        }
    }

    void OnTabChanged(TabDoc t) { RefreshTabs(); UpdateStatus(); Map.Refresh(); if (t == _cur) ScheduleLangDetect(); }

    // ------------------------------------------------------------ language guessed while you type
    Avalonia.Threading.DispatcherTimer _langTimer;

    void ScheduleLangDetect()
    {
        if (_cur == null || _cur.Path != null || _cur.LangManual) return;
        if (_langTimer == null)
        {
            _langTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _langTimer.Tick += (s2, e2) => { _langTimer.Stop(); DetectLang(); };
        }
        _langTimer.Stop(); _langTimer.Start();
    }

    /// <summary>For a new, unsaved document: recognizes the programming language from what is typed.</summary>
    void DetectLang()
    {
        if (_cur == null || _cur.Path != null || _cur.LangManual) return;
        var d = _cur.Doc;
        string guess = Syntax.Guess(d.GetText(0, Math.Min(6000, d.TextLength)));
        if (guess == _cur.Language) return;
        _cur.Language = guess;
        ApplyLanguage();
        UpdateStatus();
    }

    public async Task CloseTab(TabDoc t, bool prompt = true)
    {
        if (prompt && t.Pinned) { StFile.Text = "This tab is pinned. Unpin it to close it."; return; }
        if (prompt && !await ConfirmSave(t)) return;
        if (t.Path != null || t.Doc.TextLength > 0) _closed.Push(t);
        RemoveTab(t);
        if (_tabs.Count == 0)
        {
            if (App.Windows.Count == 1) Session.Clear();
            _allowClose = true; Close();
        }   // last tab closed: the window (and the program, if last) closes
    }

    void CapMin_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void CapMax_Click(object s, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void CapClose_Click(object s, RoutedEventArgs e) => Close();

    // empty space of the tab bar moves the window; double click maximizes it
    void TitleBar_PointerPressed(object s, PointerPressedEventArgs e)
    {
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; return; }
        BeginMoveDrag(e);
    }

    // ------------------------------------------------------------ word count and details
    async void Count_Click(object s, PointerPressedEventArgs e)
    {
        if (_cur == null) return;
        var d = Editor.Document;
        string all = d.TextLength <= 20_000_000 ? d.Text : "";
        string msg = "Document\n" + TextStats.Describe(all, d.LineCount, d.TextLength);
        if (Editor.SelectionLength > 0)
        {
            string sel = Editor.SelectedText;
            msg += "\n\nSelection\n" + TextStats.Describe(sel, sel.Split('\n').Length, sel.Length);
        }
        await MessageDialog.Show(this, msg, "OK");
    }

    // ------------------------------------------------------------ bookmarks
    void ToggleBookmark()
    {
        if (_cur == null) return;
        var d = _cur.Doc;
        int line = Editor.TextArea.Caret.Line;
        _cur.Bookmarks.RemoveAll(a => a.IsDeleted);
        var found = _cur.Bookmarks.FirstOrDefault(a => a.Line == line);
        if (found != null) _cur.Bookmarks.Remove(found);
        else _cur.Bookmarks.Add(d.CreateAnchor(d.GetLineByNumber(line).Offset));
        Editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        StFile.Text = found != null ? "Bookmark removed." : "Bookmark added. F2 goes to the next one.";
    }

    void JumpBookmark(int dir)
    {
        if (_cur == null) return;
        _cur.Bookmarks.RemoveAll(a => a.IsDeleted);
        var lines = _cur.Bookmarks.Select(a => a.Line).Distinct().OrderBy(n => n).ToList();
        if (lines.Count == 0) { StFile.Text = "No bookmarks in this document. Ctrl+F2 adds one."; return; }
        int cur = Editor.TextArea.Caret.Line;
        int target = dir > 0 ? lines.FirstOrDefault(n => n > cur) : lines.LastOrDefault(n => n < cur);
        if (target == 0) target = dir > 0 ? lines[0] : lines[^1];       // wrap around
        GoToLine(target);
    }

    void ClearBookmarks()
    {
        if (_cur == null) return;
        _cur.Bookmarks.Clear();
        Editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
    }

    // ------------------------------------------------------------ remembered position of each file
    void RememberPos(TabDoc t)
    {
        if (t == null || !ReferenceEquals(Editor.Document, t.Doc)) return;
        t.SavedOffset = Math.Clamp(Editor.CaretOffset, 0, t.Doc.TextLength);
    }

    static void StorePos(TabDoc t)
    {
        if (t.Path == null || t.SavedOffset < 0) return;
        var map = AppSettings.Current.FilePositions;
        map.Remove(t.Path);
        map[t.Path] = t.SavedOffset;
        while (map.Count > 300) map.Remove(map.Keys.First());
    }

    // ------------------------------------------------------------ clean up when saving
    void CleanBeforeSave(TabDoc t)
    {
        var s = AppSettings.Current;
        if (!s.CleanOnSave) return;
        string text = t.Doc.Text, r = text;
        if (s.CleanTrailing && t.Language != "markdown") r = TextClean.TrimTrailing(r);
        if (s.CleanDoubleSpaces) r = TextClean.DoubleSpaces(r);
        if (s.CleanTabsToSpaces) r = TextClean.TabsToSpaces(r, s.TabSize);
        if (s.CleanBlankLines) r = TextClean.ExtraBlankLines(r);
        if (r != text) DocEdit.ReplaceMinimal(t.Doc, r);
    }

    // ------------------------------------------------------------ tab menu helpers
    async Task CopyPath(TabDoc t)
    {
        if (t.Path == null) return;
        try { if (Clipboard != null) await Clipboard.SetTextAsync(t.Path); StFile.Text = "Path copied."; } catch { }
    }

    static void RevealInFolder(string path)
    {
        try
        {
            if (path == null) return;
            if (OperatingSystem.IsWindows()) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
            else if (OperatingSystem.IsMacOS()) System.Diagnostics.Process.Start("open", new[] { "-R", path });
            else System.Diagnostics.Process.Start("xdg-open", Path.GetDirectoryName(path) ?? "/");
        }
        catch { }
    }

    void TogglePin(TabDoc t)
    {
        t.Pinned = !t.Pinned;
        int old = _tabs.IndexOf(t);
        _tabs.RemoveAt(old);
        int to = _tabs.Count(x => x.Pinned);          // end of the pinned group (t itself is out of the list now)
        _tabs.Insert(to, t);
        if (to != old) TabPanel.Children.Move(old, to);
        RefreshTabs();
    }

    // ------------------------------------------------------------ print with preview
    /// <summary>Opens the document as a clean page in the default browser, whose print dialog shows the preview.</summary>
    void PrintCurrent()
    {
        if (_cur == null) return;
        try
        {
            string dir = AppSettings.DataWritable ? System.IO.Path.Combine(AppSettings.DataDir, "print") : System.IO.Path.GetTempPath();
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "notium-print-*.html"))          // tidy up earlier print pages
                try { if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddHours(-1)) File.Delete(old); } catch { }
            string file = System.IO.Path.Combine(dir, "notium-print-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".html");
            File.WriteAllText(file, Printer.BuildHtml(_cur.Title, _cur.Doc.Text, AppSettings.Current.ShowLineNumbers), new System.Text.UTF8Encoding(false));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = file, UseShellExecute = true });
            StFile.Text = "Print preview opened in your browser.";
        }
        catch (Exception ex) { StFile.Text = "Could not open the print preview: " + ex.Message; }
    }

    public void ReopenClosed()
    {
        if (_closed.Count > 0) AddTab(_closed.Pop(), true);
    }

    public void SelectTab(TabDoc t)
    {
        if (_cur != null && _cur != t) RememberPos(_cur);
        _cur = t;
        Editor.Document = t.Doc;
        if (!t.PosLoaded)
        {
            t.PosLoaded = true;
            if (t.SavedOffset < 0 && t.Path != null && AppSettings.Current.FilePositions.TryGetValue(t.Path, out int po)) t.SavedOffset = po;
        }
        if (t.SavedOffset >= 0)
        {
            int off = Math.Min(t.SavedOffset, t.Doc.TextLength);
            Editor.CaretOffset = off;
            var loc = t.Doc.GetLocation(off);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Editor.ScrollTo(loc.Line, loc.Column), Avalonia.Threading.DispatcherPriority.Loaded);
        }
        ApplyLanguage();
        RefreshTabs();
        UpdateStatus();
        Map.Refresh();
        if (_views.TryGetValue(t, out var v)) v.BringIntoView();
        Editor.Focus();
    }

    void SelectOffset(int d)
    {
        if (_tabs.Count < 2 || _cur == null) return;
        SelectTab(_tabs[(_tabs.IndexOf(_cur) + d + _tabs.Count) % _tabs.Count]);
    }

    static IBrush Res(string key) => (IBrush)Application.Current.Resources[key];

    Border BuildTabView(TabDoc t)
    {
        var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 150, FontSize = 12.5 };
        var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 7, Height = 7, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var close = new Button
        {
            Classes = { "flat", "tabclose" }, Width = 22, Height = 22, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { StrokeThickness = 1.4, StrokeLineCap = PenLineCap.Round, Data = Geometry.Parse("M0,0 L8,8 M8,0 L0,8") }
        };
        close.Click += async (s, e) => { e.Handled = true; await CloseTab(t); };
        var line = new Border { Height = 2, Width = 18, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 0, 0, 2) };
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        var pin = new Avalonia.Controls.Shapes.Path { StrokeThickness = 1.3, StrokeJoin = PenLineJoin.Round, Margin = new Thickness(6, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M2.5,0.5 L7.5,0.5 L7,4 L9,6.5 L1,6.5 L3,4 Z M5,6.5 L5,10.5") };
        sp.Children.Add(title); sp.Children.Add(dot); sp.Children.Add(close); sp.Children.Add(pin);
        var g = new Grid(); g.Children.Add(sp); g.Children.Add(line);
        var b = new Border { Child = g, Height = 30, MinWidth = 100, MaxWidth = 230, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Tag = t, Background = Brushes.Transparent };
        b.Transitions = new Avalonia.Animation.Transitions { new Avalonia.Animation.DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(120) } };
        b.PointerPressed += TabDown; b.PointerMoved += TabMove; b.PointerReleased += TabUp;
        var pinItem = new MenuItem(); var closeItem = new MenuItem { Header = "Close" };
        var copyItem = new MenuItem { Header = "Copy path" }; var folderItem = new MenuItem { Header = "Open containing folder" };
        pinItem.Click += (s, e) => TogglePin(t);
        closeItem.Click += (s, e) => _ = CloseTab(t);
        copyItem.Click += (s, e) => _ = CopyPath(t);
        folderItem.Click += (s, e) => RevealInFolder(t.Path);
        var tabMenu = new ContextMenu { Items = { pinItem, copyItem, folderItem, new Separator(), closeItem } };
        tabMenu.Opening += (s, e) =>
        {
            pinItem.Header = t.Pinned ? "Unpin tab" : "Pin tab"; closeItem.IsEnabled = !t.Pinned;
            copyItem.IsEnabled = folderItem.IsEnabled = t.Path != null;       // a new, unsaved document has no path yet
        };
        b.ContextMenu = tabMenu;
        b.PointerEntered += (s, e) => { _hover = t; RefreshTabs(); };
        b.PointerExited += (s, e) => { if (_hover == t) _hover = null; RefreshTabs(); };
        return b;
    }

    void RefreshTabs()
    {
        foreach (var t in _tabs)
        {
            if (!_views.TryGetValue(t, out var b)) continue;
            bool act = t == _cur;
            b.Background = act ? Res("ActiveTabBrush") : (t == _hover ? Res("HoverBrush") : Brushes.Transparent);
            var g = (Grid)b.Child; var sp = (StackPanel)g.Children[0]; var line = (Border)g.Children[1];
            var title = (TextBlock)sp.Children[0]; var dot = (Avalonia.Controls.Shapes.Ellipse)sp.Children[1]; var close = (Button)sp.Children[2];
            var pin = (Avalonia.Controls.Shapes.Path)sp.Children[3];
            pin.IsVisible = t.Pinned; pin.Stroke = act ? Res("TextBrush") : Res("MutedBrush");
            close.IsVisible = !t.Pinned;
            b.MinWidth = t.Pinned ? 64 : 100; title.MaxWidth = t.Pinned ? 100 : 150;
            title.Text = t.Title; ToolTip.SetTip(title, t.Path ?? t.Title);
            title.Foreground = act ? Res("TextBrush") : Res("MutedBrush");
            dot.IsVisible = t.Dirty; dot.Fill = Res("AccentBrush");
            line.IsVisible = false;
            // the X keeps its place (only its opacity changes): showing/hiding it would resize the tab under the
            // mouse pointer, which makes the tab and its buttons flicker
            bool showClose = act || t == _hover;
            close.Classes.Set("idle", !showClose);      // "idle" only hides the cross (see App.axaml)
            close.IsHitTestVisible = showClose;
        }
    }

    // ---- tab dragging (reorder, detach into a new window, move to another window)
    TabDoc _dragTab; bool _dragging; Point _dragStart; double _grabX; TabGhost _ghost;

    void TabDown(object s, PointerPressedEventArgs e)
    {
        var v = (Border)s;
        var props = e.GetCurrentPoint(v).Properties;
        var t = (TabDoc)v.Tag;
        if (props.IsMiddleButtonPressed) { _ = CloseTab(t); e.Handled = true; return; }
        if (!props.IsLeftButtonPressed) return;
        _dragTab = t; _dragging = false; _dragStart = e.GetPosition(this); _grabX = e.GetPosition(v).X;
        SelectTab(t);
        e.Pointer.Capture(v);
        e.Handled = true;
    }

    void TabMove(object s, PointerEventArgs e)
    {
        if (_dragTab == null || !e.GetCurrentPoint((Visual)s).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        if (!_dragging && (Math.Abs(p.X - _dragStart.X) > 6 || Math.Abs(p.Y - _dragStart.Y) > 6)) _dragging = true;
        if (!_dragging) return;
        var pt = e.GetPosition(TabPanel);
        _views.TryGetValue(_dragTab, out var dragView);
        if (dragView != null)
        {
            dragView.Opacity = 0.55;                      // the tab being dragged turns see-through...
            dragView.ZIndex = 10;
            dragView.RenderTransform = new TranslateTransform(pt.X - _grabX - dragView.Bounds.X, 0);   // ...and follows the mouse
        }
        bool outside = pt.Y < -20 || pt.Y > 60;
        if (outside)
        {
            // like on Windows: a small label with the tab name follows the mouse while the tab is out of the bar
            var sp = this.PointToScreen(e.GetPosition(this));
            _ghost ??= TabGhost.Open(_dragTab.Title);
            _ghost.Position = new PixelPoint(sp.X + 14, sp.Y + 18);
            return;
        }
        CloseGhost();
        double x = 0; int target = _tabs.Count - 1;
        for (int i = 0; i < _tabs.Count; i++)
        {
            double w = _views[_tabs[i]].Bounds.Width + 2;
            if (pt.X < x + w) { target = i; break; }
            x += w;
        }
        int pinned = _tabs.Count(tb => tb.Pinned);
        target = _dragTab.Pinned ? Math.Min(target, pinned - 1) : Math.Max(target, pinned);     // pinned tabs stay in their own group
        int idx = _tabs.IndexOf(_dragTab);
        if (target != idx)
        {
            _tabs.RemoveAt(idx); _tabs.Insert(target, _dragTab);
            TabPanel.Children.Move(idx, target);
            TabPanel.UpdateLayout();
            if (dragView != null) dragView.RenderTransform = new TranslateTransform(pt.X - _grabX - dragView.Bounds.X, 0);
        }
    }

    void CloseGhost() { _ghost?.Close(); _ghost = null; }

    void TabUp(object s, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        CloseGhost();
        var t = _dragTab; bool was = _dragging; _dragTab = null; _dragging = false;
        if (t != null && _views.TryGetValue(t, out var droppedView)) { droppedView.Opacity = 1; droppedView.ZIndex = 0; droppedView.RenderTransform = null; }
        if (!was || t == null) return;

        PixelPoint screen = this.PointToScreen(e.GetPosition(this));
        foreach (var w in App.Windows.ToList())
        {
            if (w.WindowState == WindowState.Minimized) continue;
            if (w.TitleScreenRect().Contains(screen))
            {
                if (w == this) return;
                RemoveTab(t);
                w.AdoptTab(t);
                if (_tabs.Count == 0) { _allowClose = true; Close(); }
                return;
            }
        }
        // dropped outside every tab bar
        var pos = new PixelPoint(screen.X - 120, screen.Y - 14);
        if (_tabs.Count == 1) { WindowState = WindowState.Normal; Position = pos; return; }
        RemoveTab(t);
        var nw = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Position = pos, Width = Width, Height = Height };
        nw.Show();
        nw.AddTab(t, true);
    }

    public PixelRect TitleScreenRect()
    {
        var tl = TabBar.PointToScreen(new Point(0, 0));
        var br = TabBar.PointToScreen(new Point(TabBar.Bounds.Width, TabBar.Bounds.Height));
        return new PixelRect(tl, br);
    }

    // ------------------------------------------------------------ settings / theme
    void OnSettingsChanged() => ApplySettings();

    public void ApplySettings()
    {
        var s = AppSettings.Current; var c = s.Colors;
        bool dark = ThemeColors.IsDark(c.Background);
        _hl.Apply(dark, _cur?.Language ?? Syntax.Plain);          // may recolor the editor: our colors come right after
        Editor.Background = ThemeColors.B(c.Background);
        Editor.Foreground = ThemeColors.B(c.Text);
        Editor.LineNumbersForeground = ThemeColors.B(c.LineNumbers);
        Editor.TextArea.SelectionBrush = ThemeColors.B(c.Selection);
        Editor.TextArea.SelectionForeground = null;
        // links keep the normal text color (not blue); Ctrl+click opens them
        Editor.Options.EnableHyperlinks = true;
        Editor.Options.EnableEmailHyperlinks = true;
        Editor.Options.RequireControlModifierForHyperlinkClick = true;
        Editor.TextArea.TextView.LinkTextForegroundBrush = ThemeColors.B(c.Text);
        Editor.TextArea.Caret.CaretBrush = ThemeColors.B(c.Caret);
        Editor.Options.HighlightCurrentLine = s.HighlightCurrentLine;
        Editor.TextArea.TextView.CurrentLineBackground = ThemeColors.B(c.CurrentLine);
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
        try { Editor.FontFamily = new FontFamily(s.FontFamily + ", " + AppSettings.FontFallback); } catch { }
        Editor.FontSize = Math.Clamp(s.FontSize, 6, 72);
        Editor.FontWeight = s.Bold ? FontWeight.Bold : FontWeight.Normal;
        Editor.Options.IndentationSize = Math.Clamp(s.TabSize, 1, 16);
        Editor.Options.ShowSpaces = s.ShowWhitespace; Editor.Options.ShowTabs = s.ShowWhitespace;
        Editor.WordWrap = s.WordWrap;
        Editor.HorizontalScrollBarVisibility = s.WordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        Editor.ShowLineNumbers = s.ShowLineNumbers;
        Editor.Padding = new Thickness(s.ShowLineNumbers ? 4 : 28, 18, 12, 12);   // numbers sit close to the left edge
        Map.IsVisible = s.ShowMinimap && !_zen;
        Map.Width = Math.Clamp(s.MinimapWidth, 60, 260);
        Map.Refresh();
        SidePanel.IsVisible = s.ShowSidebar && !_zen;
        SideBtn.Classes.Set("on", s.ShowSidebar);
        MapBtn.Classes.Set("on", s.ShowMinimap);
        if (SidePanel.IsVisible) RefreshRecent();
        TabBarScale.IsVisible = !_zen;
        StatusScale.IsVisible = s.ShowStatusBar && !_zen;
        double k = Math.Clamp(s.UiScale, 80, 160) / 100.0;
        TabBarScale.LayoutTransform = new ScaleTransform(k, k);
        StatusScale.LayoutTransform = new ScaleTransform(k, k);
        BuildEditorMenu();
        RefreshTabs();
        UpdateStatus();
    }

    void ApplyLanguage()
    {
        if (_cur == null) return;
        _hl.Apply(ThemeColors.IsDark(AppSettings.Current.Colors.Background), _cur.Language);
    }

    public void SetLanguage(string id)
    {
        if (_cur == null) return;
        _cur.Language = id; _cur.LangManual = true; ApplyLanguage(); UpdateStatus();
    }

    // ------------------------------------------------------------ status
    void UpdateStatus()
    {
        if (_cur == null) return;
        StFile.Text = "";            // the modified dot lives on the tab itself
        var car = Editor.TextArea.Caret;
        var doc = Editor.Document;
        string words = doc.TextLength <= 2_000_000 ? TextStats.Words(doc.Text).ToString() : "-";
        StCount.Text = $"{doc.LineCount} lines   {words} words   {doc.TextLength} characters"
                       + (Editor.SelectionLength > 0 ? $"   ({Editor.SelectionLength} selected)" : "");
        BtnPos.Content = $"Ln {car.Line}, Col {car.Column}";
        BtnLang.Content = Syntax.Display(_cur.Language);
        BtnEol.Content = _cur.Eol;
        BtnEnc.Content = _cur.Encoding;
        BtnZoom.Content = Math.Round(AppSettings.Current.FontSize / 14.0 * 100) + "%";
        Title = (_cur.Dirty ? "● " : "") + _cur.Title + " - Notium";
    }

    // ------------------------------------------------------------ external changes
    async Task CheckExternalChangesAsync()
    {
        if (_checkingExternal) return;
        _checkingExternal = true;
        try
        {
            foreach (var t in _tabs.ToList())
            {
                if (t.Path == null || !File.Exists(t.Path)) continue;
                var wt = File.GetLastWriteTimeUtc(t.Path);
                if (wt == t.DiskTime) continue;
                t.DiskTime = wt;
                // untouched documents follow the file silently; modified ones ask first
                if (!(AppSettings.Current.AutoReload && !t.Dirty))
                {
                    int r = await MessageDialog.Show(this, $"The file \"{t.Title}\" was modified by another program.\nDo you want to reload it?", "Reload", "Keep my version");
                    if (r != 0) continue;
                }
                try
                {
                    var n = TabDoc.FromFile(t.Path);
                    t.Encoding = n.Encoding; t.Eol = n.Eol;
                    t.SetText(n.Doc.Text); t.Dirty = false; t.DiskTime = n.DiskTime;
                    t.RaiseChanged();
                }
                catch (Exception ex) { await MessageDialog.Error(this, ex.Message); }
            }
        }
        finally { _checkingExternal = false; }
    }

    void OnDropFiles(object sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files == null) return;
        foreach (var f in files)
        {
            string p = f.TryGetLocalPath();
            if (p != null && File.Exists(p)) OpenPath(p);
        }
    }
}
}

// ---- MainWindow.Commands.cs
namespace Notium
{
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

public partial class MainWindow
{
    // ------------------------------------------------------------ keyboard
    // The shortcut of every command can be changed in the settings (see Keymap.cs).
    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _zen) { ToggleZen(); e.Handled = true; return; }
        if (e.Key == Key.Escape && FindPanel.IsVisible) { FindClose_Click(null, null); e.Handled = true; return; }
        string g = Keymap.FromEvent(e);
        if (g == null) return;
        string id = Keymap.FindId(g);
        if (id == null) return;
        if ((id == "move-up" || id == "move-down") && !Editor.TextArea.IsFocused) return;
        if (_cmd.TryGetValue(id, out var run)) { run(); e.Handled = true; }
    }

    static KeyGesture K(string id) => Keymap.Gesture(id);

    void Settings_Click(object s, RoutedEventArgs e) => SettingsWindow.ShowSingle(this);
    void NewTab_Click(object s, RoutedEventArgs e) => NewTab();
    void Pos_Click(object s, RoutedEventArgs e) => _ = GoToLineAsync();
    void Lang_Click(object s, RoutedEventArgs e) => _ = ChooseLanguageAsync();
    void Eol_Click(object s, RoutedEventArgs e) => BuildEolMenu().Open(BtnEol);
    void Enc_Click(object s, RoutedEventArgs e) => BuildEncodingMenu().Open(BtnEnc);
    void Zoom_Click(object s, RoutedEventArgs e) => ResetZoom();

    ContextMenu BuildEolMenu()
    {
        var m = new ContextMenu();
        foreach (var (n, l) in new[] { ("Windows (CRLF)", "CRLF"), ("Unix (LF)", "LF"), ("Mac (CR)", "CR") })
        { var ll = l; m.Items.Add(Mi(n, () => SetEol(ll), null, _cur?.Eol == ll)); }
        return m;
    }

    MenuItem EolItem()
    {
        var item = new MenuItem { Header = "Line ending" };
        foreach (var (n, l) in new[] { ("Windows (CRLF)", "CRLF"), ("Unix (LF)", "LF"), ("Mac (CR)", "CR") })
        { var ll = l; item.Items.Add(Mi(n, () => SetEol(ll), null, _cur?.Eol == ll)); }
        return item;
    }

    void SetEol(string eol)
    {
        if (_cur == null || _cur.Eol == eol) return;
        _cur.Eol = eol; _cur.Dirty = true; _cur.RaiseChanged();
    }

    ContextMenu BuildEncodingMenu()
    {
        var m = new ContextMenu();
        var re = new MenuItem { Header = "Reopen with encoding" };
        foreach (var n in Enc.Names) { var nn = n; re.Items.Add(Mi(nn, () => _ = ReopenWithEncoding(nn), null, _cur?.Encoding == nn)); }
        var sv = new MenuItem { Header = "Save with encoding" };
        foreach (var n in Enc.Names)
        { var nn = n; sv.Items.Add(Mi(nn, () => { if (_cur != null && _cur.Encoding != nn) { _cur.Encoding = nn; _cur.Dirty = true; _cur.RaiseChanged(); } }, null, _cur?.Encoding == nn)); }
        m.Items.Add(re); m.Items.Add(sv);
        return m;
    }

    async Task ReopenWithEncoding(string name)
    {
        var t = _cur;
        if (t?.Path == null || !File.Exists(t.Path))
        { await MessageDialog.Error(this, "Only files saved on disk can be reopened with another encoding."); return; }
        if (t.Dirty && await MessageDialog.Show(this, "Reopening discards your unsaved changes. Continue?", "Reopen", "Cancel") != 0) return;
        try
        {
            byte[] b = await Task.Run(() => File.ReadAllBytes(t.Path));
            string text = Enc.DecodeAs(b, name);
            t.Encoding = name; t.Eol = Enc.DetectEol(text);
            t.SetText(Enc.Normalize(text));
            t.Dirty = false; t.DiskTime = File.GetLastWriteTimeUtc(t.Path);
            t.RaiseChanged();
        }
        catch (Exception ex) { await MessageDialog.Error(this, ex.Message); }
    }

    async Task ChooseLanguageAsync()
    {
        if (_cur == null) return;
        string id = await LanguageDialog.Choose(this, _cur.Language);
        if (id != null) SetLanguage(id);
    }

    // ------------------------------------------------------------ zoom and distraction-free mode
    void Zoom(int d)
    {
        var s = AppSettings.Current;
        s.FontSize = Math.Clamp(Math.Round(s.FontSize) + d, 6, 72);
        AppSettings.Notify();
    }

    void ResetZoom() { AppSettings.Current.FontSize = 14; AppSettings.Notify(); }

    void OnEditorWheel(object sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0) return;
        Zoom(e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    void ToggleZen()
    {
        _zen = !_zen;
        if (_zen) { _stateBeforeZen = WindowState; WindowState = WindowState.FullScreen; }
        else WindowState = _stateBeforeZen == WindowState.FullScreen ? WindowState.Normal : _stateBeforeZen;
        Editor.MaxWidth = _zen ? 920 : double.PositiveInfinity;   // centered column of text
        ApplySettings();
    }

    // ------------------------------------------------------------ files
    async Task OpenDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = new[] { FilePickerFileTypes.All }
        });
        foreach (var f in files)
        {
            string p = f.TryGetLocalPath();
            if (p != null) await OpenPathAsync(p);
        }
    }

    public void OpenPath(string path) => _ = OpenPathAsync(path);

    public async Task OpenPathAsync(string path)
    {
        try
        {
            foreach (var w in App.Windows.ToList())
            {
                var ex = w.FindTab(path);                 // already open, maybe in another window: go there
                if (ex != null) { w.ShowTab(ex); return; }
            }
            var fi = new FileInfo(path);
            if (fi.Length > 100L * 1024 * 1024 &&
                await MessageDialog.Show(this, $"\"{fi.Name}\" is very large ({fi.Length / 1024 / 1024} MB) and may be slow to edit.\nOpen it anyway?", "Open", "Cancel") != 0)
                return;
            TabDoc nt;
            if (fi.Length > 2L * 1024 * 1024)
            {
                StFile.Text = "Opening " + fi.Name + "...";
                var lf = await Task.Run(() => TabDoc.Read(path));       // reading and decoding happen off the UI thread
                nt = TabDoc.FromLoaded(lf);
            }
            else nt = TabDoc.FromFile(path);
            if (_tabs.Count == 1 && _cur != null && _cur.Path == null && !_cur.Dirty && _cur.Doc.TextLength == 0)
            {
                var old = _cur; AddTab(nt, true); RemoveTab(old);
            }
            else AddTab(nt, true);
            AddRecent(path);
        }
        catch (Exception e) { await MessageDialog.Error(this, "Could not open the file:\n" + e.Message); }
    }

    void AddRecent(string p)
    {
        var r = AppSettings.Current.Recent;
        r.RemoveAll(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase));
        r.Insert(0, p);
        if (r.Count > 12) r.RemoveRange(12, r.Count - 12);
        AppSettings.Save();
        RefreshRecent();
    }

    public async Task<bool> SaveTab(TabDoc t)
    {
        if (t.Path == null) return await SaveAs(t);
        CleanBeforeSave(t);
        try { t.Save(); }
        catch (Exception e) { await MessageDialog.Error(this, "Saving failed:\n" + e.Message); return false; }
        t.RaiseChanged();
        return true;
    }

    /// <summary>Documents are .txt by default. To save as any other type (.py, .js, .html, ...) just type the extension in the file name.</summary>
    async Task<bool> SaveAs(TabDoc t)
    {
        // a new document is saved as .txt, or with the usual extension of the language it was recognized as (.py, .js...)
        string ext = Syntax.DefaultExt(t.Language);
        string kind = ext == "txt" ? "Text document" : Syntax.Display(t.Language);
        var opts = new FilePickerSaveOptions
        {
            SuggestedFileName = t.Path != null ? Path.GetFileName(t.Path) : t.Title + "." + ext,
            DefaultExtension = ext,
            FileTypeChoices = new[] { new FilePickerFileType($"{kind} (.{ext}) - type another extension in the name to change it") { Patterns = new[] { "*" } } },
            ShowOverwritePrompt = true
        };
        try
        {
            string dir = t.Path != null ? Path.GetDirectoryName(t.Path) : (SidebarFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (!string.IsNullOrEmpty(dir)) opts.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(dir);
        }
        catch { }
        var f = await StorageProvider.SaveFilePickerAsync(opts);
        string path = f?.TryGetLocalPath();
        if (path == null) return false;
        if (string.IsNullOrEmpty(Path.GetExtension(path))) path += "." + ext;
        CleanBeforeSave(t);
        try { t.Save(path); }
        catch (Exception e) { await MessageDialog.Error(this, "Saving failed:\n" + e.Message); return false; }
        AddRecent(path);
        ApplyLanguage();
        t.RaiseChanged();
        return true;
    }

    // ------------------------------------------------------------ menus
    static MenuItem Mi(string header, Action a, KeyGesture gesture = null, bool? check = null)
    {
        var i = new MenuItem { Header = header, InputGesture = gesture };
        if (check != null) { i.ToggleType = MenuItemToggleType.CheckBox; i.IsChecked = check.Value; }
        if (a != null) i.Click += (s, e) => a();
        return i;
    }

    void BuildEditorMenu()
    {
        var m = new ContextMenu();
        m.Items.Add(Mi("Cut", () => Editor.Cut()));
        m.Items.Add(Mi("Copy", () => Editor.Copy()));
        m.Items.Add(Mi("Paste", () => Editor.Paste()));
        m.Items.Add(new Separator());
        m.Items.Add(Mi("Undo", () => Editor.Undo()));
        m.Items.Add(Mi("Redo", () => Editor.Redo()));
        m.Items.Add(new Separator());
        m.Items.Add(Mi("Select all", () => Editor.SelectAll()));
        m.Items.Add(Mi("Toggle comment", ToggleComment, K("comment")));
        m.Items.Add(TransformItem());
        m.Items.Add(Mi("Word wrap", () => { AppSettings.Current.WordWrap = !AppSettings.Current.WordWrap; AppSettings.Notify(); }, null, AppSettings.Current.WordWrap));
        Editor.ContextMenu = m;
    }

    MenuItem TransformItem()
    {
        var t = new MenuItem { Header = "Transform (selection or whole document)" };
        t.Items.Add(Mi("UPPERCASE", () => Transform(s => s.ToUpperInvariant(), false)));
        t.Items.Add(Mi("lowercase", () => Transform(s => s.ToLowerInvariant(), false)));
        t.Items.Add(Mi("Title Case", () => Transform(s => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant()), false)));
        t.Items.Add(new Separator());
        t.Items.Add(Mi("Sort lines (A to Z)", () => Transform(LineOp(l => l.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)), true)));
        t.Items.Add(Mi("Sort lines (Z to A)", () => Transform(LineOp(l => l.OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)), true)));
        t.Items.Add(Mi("Reverse lines", () => Transform(LineOp(l => Enumerable.Reverse(l)), true)));
        t.Items.Add(Mi("Remove duplicate lines", () => Transform(LineOp(l => l.Distinct()), true)));
        t.Items.Add(Mi("Remove empty lines", () => Transform(LineOp(l => l.Where(x => x.Trim().Length > 0)), true)));
        t.Items.Add(Mi("Trim trailing spaces", () => Transform(LineOp(l => l.Select(x => x.TrimEnd(' ', '\t', '\r'))), true)));
        t.Items.Add(new Separator());
        t.Items.Add(Mi("Remove double spaces", () => Transform(TextClean.DoubleSpaces, true)));
        t.Items.Add(Mi("Keep at most one empty line in a row", () => Transform(s => TextClean.ExtraBlankLines(TextClean.TrimTrailing(s)), true)));
        t.Items.Add(Mi("Tabs to spaces", () => Transform(s => TextClean.TabsToSpaces(s, AppSettings.Current.TabSize), true)));
        t.Items.Add(Mi("Spaces to tabs (indentation)", () => Transform(s => TextClean.SpacesToTabs(s, AppSettings.Current.TabSize), true)));
        t.Items.Add(Mi("Capitalize sentences", () => Transform(TextClean.SmartCase, false)));
        return t;
    }

    void WireFind()
    {
        FindBox.PropertyChanged += (s, e) =>
        {
            if (e.Property == TextBox.TextProperty) { UpdateFindCount(); FindStep(true, true); }
        };
    }

    void MenuBtn_Click(object s, RoutedEventArgs e) => BuildMenu().Open(MenuBtn);

    ContextMenu BuildMenu()
    {
        var st = AppSettings.Current;
        var m = new ContextMenu();

        var file = new MenuItem { Header = "File" };
        file.Items.Add(Mi("New tab", NewTab, K("new-tab")));
        file.Items.Add(Mi("New window", () => new MainWindow().Show(), K("new-window")));
        file.Items.Add(Mi("Open...", () => _ = OpenDialogAsync(), K("open")));
        file.Items.Add(Mi("Open folder...", () => _ = OpenFolderAsync()));
        var rec = new MenuItem { Header = "Recent documents" };
        foreach (var p in st.Recent.ToList()) { var pp = p; rec.Items.Add(Mi(pp, () => { if (File.Exists(pp)) OpenPath(pp); })); }
        if (rec.Items.Count == 0) rec.IsEnabled = false;
        file.Items.Add(rec);
        file.Items.Add(new Separator());
        file.Items.Add(Mi("Save", () => { if (_cur != null) _ = SaveTab(_cur); }, K("save")));
        file.Items.Add(Mi("Save as...", () => { if (_cur != null) _ = SaveAs(_cur); }, K("save-as")));
        file.Items.Add(Mi("Print... (preview)", PrintCurrent, K("print")));
        file.Items.Add(new Separator());
        file.Items.Add(Mi("Close tab", () => { if (_cur != null) _ = CloseTab(_cur); }, K("close-tab")));
        file.Items.Add(Mi("Reopen closed tab", ReopenClosed, K("reopen-tab")));
        file.Items.Add(Mi("Pin / unpin tab", () => { if (_cur != null) TogglePin(_cur); }, K("pin-tab")));
        file.Items.Add(new Separator());
        file.Items.Add(Mi("Exit", () => _ = ExitAllAsync()));
        m.Items.Add(file);

        var edit = new MenuItem { Header = "Edit" };
        edit.Items.Add(Mi("Undo", () => Editor.Undo()));
        edit.Items.Add(Mi("Redo", () => Editor.Redo()));
        edit.Items.Add(new Separator());
        edit.Items.Add(Mi("Cut", () => Editor.Cut()));
        edit.Items.Add(Mi("Copy", () => Editor.Copy()));
        edit.Items.Add(Mi("Paste", () => Editor.Paste()));
        edit.Items.Add(Mi("Select all", () => Editor.SelectAll()));
        edit.Items.Add(new Separator());
        edit.Items.Add(Mi("Find...", () => ShowFind(false), K("find")));
        edit.Items.Add(Mi("Replace...", () => ShowFind(true), K("replace")));
        edit.Items.Add(Mi("Find and replace in files...", ShowFindInFiles, K("find-in-files")));
        edit.Items.Add(Mi("Go to line...", () => _ = GoToLineAsync(), K("goto-line")));
        var bm = new MenuItem { Header = "Bookmarks" };
        bm.Items.Add(Mi("Toggle bookmark", ToggleBookmark, K("bookmark")));
        bm.Items.Add(Mi("Next bookmark", () => JumpBookmark(1), K("bookmark-next")));
        bm.Items.Add(Mi("Previous bookmark", () => JumpBookmark(-1), K("bookmark-prev")));
        bm.Items.Add(Mi("Clear bookmarks of this document", ClearBookmarks));
        edit.Items.Add(bm);
        edit.Items.Add(new Separator());
        edit.Items.Add(Mi("Duplicate line", DuplicateLine, K("duplicate-line")));
        edit.Items.Add(Mi("Delete line", DeleteLine, K("delete-line")));
        edit.Items.Add(Mi("Move line up", () => MoveLines(-1), K("move-up")));
        edit.Items.Add(Mi("Move line down", () => MoveLines(1), K("move-down")));
        edit.Items.Add(Mi("Toggle comment", ToggleComment, K("comment")));
        edit.Items.Add(TransformItem());
        edit.Items.Add(Mi("Insert date and time", InsertDateTime, K("insert-datetime")));
        m.Items.Add(edit);

        var fmt = new MenuItem { Header = "Format" };
        var enc = BuildEncodingMenu();
        var encItems = enc.Items.OfType<MenuItem>().ToList();
        enc.Items.Clear();                       // move the two encoding sub-menus into the Format menu
        foreach (var it in encItems) fmt.Items.Add(it);
        fmt.Items.Add(EolItem());
        fmt.Items.Add(Mi("Language...", () => _ = ChooseLanguageAsync()));
        fmt.Items.Add(Mi("Word wrap", () => { st.WordWrap = !st.WordWrap; AppSettings.Notify(); }, null, st.WordWrap));
        m.Items.Add(fmt);

        var view = new MenuItem { Header = "View" };
        view.Items.Add(Mi("Folder panel", ToggleSidebar, K("sidebar"), st.ShowSidebar));
        view.Items.Add(Mi("Distraction-free mode", ToggleZen, K("zen")));
        view.Items.Add(new Separator());
        view.Items.Add(Mi("Zoom in", () => Zoom(1), K("zoom-in")));
        view.Items.Add(Mi("Zoom out", () => Zoom(-1), K("zoom-out")));
        view.Items.Add(Mi("Reset zoom", ResetZoom, K("zoom-reset")));
        view.Items.Add(new Separator());
        view.Items.Add(Mi("Line numbers", () => { st.ShowLineNumbers = !st.ShowLineNumbers; AppSettings.Notify(); }, null, st.ShowLineNumbers));
        view.Items.Add(Mi("Minimap", () => { st.ShowMinimap = !st.ShowMinimap; AppSettings.Notify(); }, null, st.ShowMinimap));
        view.Items.Add(Mi("Visible spaces and tabs", () => { st.ShowWhitespace = !st.ShowWhitespace; AppSettings.Notify(); }, null, st.ShowWhitespace));
        view.Items.Add(Mi("Highlight current line", () => { st.HighlightCurrentLine = !st.HighlightCurrentLine; AppSettings.Notify(); }, null, st.HighlightCurrentLine));
        view.Items.Add(Mi("Status bar", () => { st.ShowStatusBar = !st.ShowStatusBar; AppSettings.Notify(); }, null, st.ShowStatusBar));
        m.Items.Add(view);

        m.Items.Add(new Separator());
        m.Items.Add(Mi("Settings", () => SettingsWindow.ShowSingle(this), K("settings")));
        return m;
    }

    // ------------------------------------------------------------ find / replace
    void ShowFind(bool replace)
    {
        bool wasHidden = !FindPanel.IsVisible;
        FindPanel.IsVisible = true;
        if (wasHidden) Fx.FadeIn(FindPanel, 120);
        ReplaceRow.IsVisible = replace;
        string sel = Editor.SelectedText;
        if (!string.IsNullOrEmpty(sel) && !sel.Contains('\n')) FindBox.Text = sel;
        FindBox.Focus(); FindBox.SelectAll();
    }

    void FindClose_Click(object s, RoutedEventArgs e) { FindPanel.IsVisible = false; Editor.Focus(); }
    void FindNext_Click(object s, RoutedEventArgs e) => FindStep(true);
    void FindPrev_Click(object s, RoutedEventArgs e) => FindStep(false);
    void FindOpt_Click(object s, RoutedEventArgs e) => UpdateFindCount();

    void FindBox_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { FindStep((e.KeyModifiers & KeyModifiers.Shift) == 0); e.Handled = true; }
    }

    void ReplaceBox_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ReplaceOne_Click(null, null); e.Handled = true; }
    }

    Regex BuildRegex()
    {
        string pat = FindBox.Text;
        if (string.IsNullOrEmpty(pat)) return null;
        if (RegexChk.IsChecked != true) pat = Regex.Escape(pat);
        var opt = RegexOptions.Multiline | (CaseChk.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase);
        try { return new Regex(pat, opt); }
        catch { FindInfo.Text = "Invalid expression"; return null; }
    }

    void UpdateFindCount()
    {
        var rx = BuildRegex();
        if (rx == null) { if (string.IsNullOrEmpty(FindBox.Text)) FindInfo.Text = ""; return; }
        int n = rx.Matches(Editor.Text).Count;
        FindInfo.Text = n == 0 ? "No results" : n + (n == 1 ? " result" : " results");
    }

    void FindStep(bool forward, bool fromSelStart = false)
    {
        var rx = BuildRegex(); if (rx == null) return;
        string text = Editor.Text;
        int selStart = Editor.SelectionStart, selEnd = selStart + Editor.SelectionLength;
        Match m;
        if (forward)
        {
            int start = fromSelStart ? selStart : selEnd;
            m = rx.Match(text, Math.Min(start, text.Length));
            if (!m.Success) m = rx.Match(text, 0);
        }
        else
        {
            Match last = null;
            foreach (Match x in rx.Matches(text))
            {
                if (x.Index + x.Length <= selStart) last = x; else break;
            }
            if (last == null) foreach (Match x in rx.Matches(text)) last = x;
            m = last;
        }
        if (m != null && m.Success)
        {
            Editor.Select(m.Index, m.Length);
            Editor.TextArea.Caret.Offset = m.Index + m.Length;
            Editor.TextArea.Caret.BringCaretToView();
            Editor.ScrollTo(Editor.Document.GetLineByOffset(m.Index).LineNumber, 0);
        }
        else FindInfo.Text = "No results";
    }

    string Repl(string r) => RegexChk.IsChecked == true ? r : r.Replace("$", "$$");

    void ReplaceOne_Click(object s, RoutedEventArgs e)
    {
        var rx = BuildRegex(); if (rx == null) return;
        string sel = Editor.SelectedText;
        var m = rx.Match(sel);
        if (sel.Length > 0 && m.Success && m.Index == 0 && m.Length == sel.Length)
            Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, rx.Replace(sel, Repl(ReplaceBox.Text ?? ""), 1));
        UpdateFindCount();
        FindStep(true);
    }

    void ReplaceAll_Click(object s, RoutedEventArgs e)
    {
        var rx = BuildRegex(); if (rx == null) return;
        string text = Editor.Text;
        int n = rx.Matches(text).Count;
        if (n == 0) { FindInfo.Text = "No results"; return; }
        string res = rx.Replace(text, Repl(ReplaceBox.Text ?? ""));
        Editor.Document.Replace(0, text.Length, res);
        FindInfo.Text = n + (n == 1 ? " replacement" : " replacements");
    }
}
}

// ---- MainWindow.Tools.cs
namespace Notium
{
using Avalonia.Controls;
using AvaloniaEdit.Document;

/// <summary>Editing helpers: go to line, move line, comment, transform, find in files, line operations.</summary>
public partial class MainWindow
{
    // ------------------------------------------------------------ go to line
    async Task GoToLineAsync()
    {
        if (_cur == null) return;
        string r = await InputDialog.Ask(this, "Go to line", $"Line number (1 - {Editor.Document.LineCount}), or line:column", Editor.TextArea.Caret.Line.ToString());
        if (string.IsNullOrWhiteSpace(r)) return;
        var parts = r.Trim().Split(':');
        if (!int.TryParse(parts[0], out int line)) return;
        int col = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 1;
        GoToLine(line, col);
    }

    public void GoToLine(int line, int col = 1)
    {
        if (line < 1) return;
        var d = Editor.Document;
        line = Math.Min(line, d.LineCount);
        var l = d.GetLineByNumber(line);
        int off = l.Offset + Math.Clamp(col - 1, 0, l.Length);
        Editor.Select(off, 0);
        Editor.TextArea.Caret.Offset = off;
        Editor.TextArea.Caret.BringCaretToView();
        Editor.ScrollTo(line, Math.Max(1, col));
        Editor.Focus();
    }

    // ------------------------------------------------------------ find in files
    void ShowFindInFiles()
    {
        if (_filesWin != null) { _filesWin.Activate(); return; }
        _filesWin = new FindInFilesWindow(this);
        _filesWin.Closed += (s, e) => _filesWin = null;
        _filesWin.Show(this);
    }

    public async Task OpenHit(Hit h)
    {
        if (h.Tab != null && _tabs.Contains(h.Tab)) SelectTab(h.Tab);
        else if (h.Path != null) await OpenPathAsync(h.Path);
        else return;
        GoToLine(h.Line);
    }

    // ------------------------------------------------------------ lines
    /// <summary>First and last line (1-based) touched by the selection, or the caret line.</summary>
    (int first, int last) SelectedLines()
    {
        var d = Editor.Document;
        int len = Editor.SelectionLength;
        if (len == 0) { int cl = Editor.TextArea.Caret.Line; return (cl, cl); }
        int s = Editor.SelectionStart, e = s + len;
        int fl = d.GetLineByOffset(s).LineNumber;
        int ll = d.GetLineByOffset(e).LineNumber;
        if (ll > fl && e == d.GetLineByNumber(ll).Offset) ll--;     // a selection ending at the start of a line does not include it
        return (fl, ll);
    }

    void MoveLines(int dir)
    {
        if (_cur == null) return;
        var d = Editor.Document;
        var (fl, ll) = SelectedLines();
        if (dir < 0 && fl == 1) return;
        if (dir > 0 && ll == d.LineCount) return;

        int selStart = Editor.SelectionStart, selLen = Editor.SelectionLength, caret = Editor.TextArea.Caret.Offset;
        int blockStart = d.GetLineByNumber(fl).Offset;
        int blockEnd = d.GetLineByNumber(ll).EndOffset;
        string block = d.GetText(blockStart, blockEnd - blockStart);
        int newStart;
        if (dir < 0)
        {
            var prev = d.GetLineByNumber(fl - 1);
            string pt = d.GetText(prev.Offset, prev.Length);
            d.Replace(prev.Offset, blockEnd - prev.Offset, block + "\n" + pt);
            newStart = prev.Offset;
        }
        else
        {
            var next = d.GetLineByNumber(ll + 1);
            string nt = d.GetText(next.Offset, next.Length);
            d.Replace(blockStart, next.EndOffset - blockStart, nt + "\n" + block);
            newStart = blockStart + nt.Length + 1;
        }
        if (selLen > 0) Editor.Select(newStart + (selStart - blockStart), selLen);
        else { Editor.Select(newStart + (caret - blockStart), 0); Editor.TextArea.Caret.Offset = newStart + (caret - blockStart); }
        Editor.TextArea.Caret.BringCaretToView();
    }

    // ------------------------------------------------------------ comments (the marker depends on the language)
    void ToggleComment()
    {
        if (_cur == null) return;
        var (open, close) = Syntax.Comment(_cur.Language);
        var d = Editor.Document;
        var (fl, ll) = SelectedLines();
        var lines = new List<DocumentLine>();
        for (int i = fl; i <= ll; i++) lines.Add(d.GetLineByNumber(i));
        var filled = lines.Where(l => d.GetText(l.Offset, l.Length).Trim().Length > 0).ToList();
        if (filled.Count == 0) return;
        bool allCommented = filled.All(l => IsCommented(d.GetText(l.Offset, l.Length), open, close));

        d.BeginUpdate();
        try
        {
            for (int i = lines.Count - 1; i >= 0; i--)              // bottom to top keeps the offsets valid
            {
                var l = lines[i];
                string t = d.GetText(l.Offset, l.Length);
                if (t.Trim().Length == 0) continue;
                string nt = allCommented ? Uncomment(t, open, close) : AddComment(t, open, close);
                if (nt != t) d.Replace(l.Offset, l.Length, nt);
            }
        }
        finally { d.EndUpdate(); }
    }

    static bool IsCommented(string line, string open, string close)
    {
        string t = line.Trim();
        return t.StartsWith(open, StringComparison.Ordinal) && (close.Length == 0 || t.EndsWith(close, StringComparison.Ordinal));
    }

    static string AddComment(string line, string open, string close)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
        string indent = line.Substring(0, i), rest = line.Substring(i);
        return indent + open + " " + rest + (close.Length > 0 ? " " + close : "");
    }

    static string Uncomment(string line, string open, string close)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
        string indent = line.Substring(0, i), rest = line.Substring(i);
        if (rest.StartsWith(open, StringComparison.Ordinal)) rest = rest.Substring(open.Length);
        if (rest.StartsWith(" ")) rest = rest.Substring(1);
        if (close.Length > 0)
        {
            rest = rest.TrimEnd();
            if (rest.EndsWith(close, StringComparison.Ordinal)) rest = rest.Substring(0, rest.Length - close.Length);
            rest = rest.TrimEnd();
        }
        return indent + rest;
    }

    // ------------------------------------------------------------ transform text
    static Func<string, string> LineOp(Func<List<string>, IEnumerable<string>> f) => s =>
    {
        bool nl = s.EndsWith("\n");
        var ls = (nl ? s.Substring(0, s.Length - 1) : s).Split('\n').ToList();
        string r = string.Join("\n", f(ls));
        return nl ? r + "\n" : r;
    };

    /// <summary>Applies a change to the selection, or to the whole document when nothing is selected.</summary>
    void Transform(Func<string, string> f, bool wholeLines)
    {
        if (_cur == null) return;
        var d = Editor.Document;
        int start, len;
        if (wholeLines)
        {
            int fl, ll;
            if (Editor.SelectionLength == 0) { fl = 1; ll = d.LineCount; } else (fl, ll) = SelectedLines();
            start = d.GetLineByNumber(fl).Offset;
            len = d.GetLineByNumber(ll).EndOffset - start;
        }
        else if (Editor.SelectionLength == 0) { start = 0; len = d.TextLength; }
        else { start = Editor.SelectionStart; len = Editor.SelectionLength; }
        string old = d.GetText(start, len);
        string nw = f(old);
        if (nw != old) d.Replace(start, len, nw);
    }

    // ------------------------------------------------------------ single-line editing
    void DeleteLine()
    {
        var d = Editor.Document; var l = d.GetLineByNumber(Editor.TextArea.Caret.Line);
        d.Remove(l.Offset, l.TotalLength);
    }

    void DuplicateLine()
    {
        var d = Editor.Document; var l = d.GetLineByNumber(Editor.TextArea.Caret.Line);
        d.Insert(l.EndOffset, "\n" + d.GetText(l.Offset, l.Length));
    }

    void InsertDateTime()
    {
        Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, DateTime.Now.ToString("HH:mm dd/MM/yyyy"));
    }
}
}

// ---- MainWindow.Sidebar.cs
namespace Notium
{
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

public partial class MainWindow
{
    TreeView _tree;
    ListBox _recentList;
    TextBlock _folderName;

    public string SidebarFolder { get; private set; }
    public IReadOnlyList<TabDoc> Tabs => _tabs;

    static Avalonia.Controls.Shapes.Path MakeIcon(string data, double size = 14)
        => new() { Data = Geometry.Parse(data), Stroke = Res("TextBrush"), StrokeThickness = 1.5, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Stretch = Stretch.None, Width = size, Height = size };

    /// <summary>Left panel: the files of a folder and the recent documents.</summary>
    Control BuildSidebar()
    {
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), Margin = new Thickness(10, 10, 10, 8) };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        _folderName = new TextBlock { Text = "No folder open", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var open = new Button { Classes = { "flat" }, Padding = new Thickness(8, 5), Content = MakeIcon("M1,3 L6,3 L8,5 L14,5 L14,13 L1,13 Z") };
        ToolTip.SetTip(open, "Open a folder");
        open.Click += async (s, e) => await OpenFolderAsync();
        head.Children.Add(_folderName);
        Grid.SetColumn(open, 1); head.Children.Add(open);
        g.Children.Add(head);

        _tree = new TreeView { Background = Brushes.Transparent };
        _tree.DoubleTapped += (s, e) =>
        {
            if (_tree.SelectedItem is TreeViewItem ti && ti.Tag is string p && File.Exists(p)) OpenPath(p);
        };
        Grid.SetRow(_tree, 1); g.Children.Add(_tree);

        var rh = new TextBlock { Text = "Recent", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 10, 0, 6) };
        Grid.SetRow(rh, 2); g.Children.Add(rh);
        _recentList = new ListBox { Background = Brushes.Transparent, MaxHeight = 190 };
        _recentList.DoubleTapped += (s, e) =>
        {
            if (_recentList.SelectedItem is ListBoxItem li && li.Tag is string p && File.Exists(p)) OpenPath(p);
        };
        Grid.SetRow(_recentList, 3); g.Children.Add(_recentList);

        if (!string.IsNullOrEmpty(AppSettings.Current.LastFolder) && Directory.Exists(AppSettings.Current.LastFolder))
            SetFolder(AppSettings.Current.LastFolder, false);
        RefreshRecent();
        return g;
    }

    public async Task OpenFolderAsync()
    {
        var r = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Open a folder" });
        string p = r.FirstOrDefault()?.TryGetLocalPath();
        if (p == null) return;
        SetFolder(p, true);
        if (!AppSettings.Current.ShowSidebar) { AppSettings.Current.ShowSidebar = true; AppSettings.Notify(); }
    }

    void SetFolder(string path, bool save)
    {
        SidebarFolder = path;
        _folderName.Text = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(_folderName.Text)) _folderName.Text = path;
        ToolTip.SetTip(_folderName, path);
        FillDir(_tree, path);
        if (save) { AppSettings.Current.LastFolder = path; AppSettings.Save(); }
    }

    static void FillDir(ItemsControl host, string dir)
    {
        host.Items.Clear();
        try
        {
            foreach (var d in Directory.EnumerateDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                string n = Path.GetFileName(d);
                if (n.StartsWith('.')) continue;
                var ti = new TreeViewItem { Header = n + "/" };
                string full = d;
                ti.Items.Add(new TreeViewItem { Header = "..." });
                bool loaded = false;
                ti.PropertyChanged += (s, e) =>
                {
                    if (e.Property == TreeViewItem.IsExpandedProperty && ti.IsExpanded && !loaded) { loaded = true; FillDir(ti, full); }
                };
                host.Items.Add(ti);
            }
            int count = 0;
            foreach (var f in Directory.EnumerateFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (++count > 1000) break;
                host.Items.Add(new TreeViewItem { Header = Path.GetFileName(f), Tag = f });
            }
        }
        catch { }
    }

    void RefreshRecent()
    {
        if (_recentList == null) return;
        _recentList.Items.Clear();
        foreach (var p in AppSettings.Current.Recent.ToList())
        {
            var li = new ListBoxItem { Content = Path.GetFileName(p), Tag = p };
            ToolTip.SetTip(li, p);
            _recentList.Items.Add(li);
        }
    }

    void ToggleSidebar()
    {
        AppSettings.Current.ShowSidebar = !AppSettings.Current.ShowSidebar;
        AppSettings.Notify();
    }

    void Sidebar_Click(object s, RoutedEventArgs e) => ToggleSidebar();

    void MapBtn_Click(object s, RoutedEventArgs e)
    {
        AppSettings.Current.ShowMinimap = !AppSettings.Current.ShowMinimap;
        AppSettings.Notify();
    }
}
}
