// ---- Settings.cs
namespace Notium
{
using System.Text.Json;
using Avalonia.Media;

public class ThemeColors
{
    public string Name { get; set; } = "Dark";
    public string Background { get; set; } = "#1C1C1C";
    public string Text { get; set; } = "#E6E6E6";
    public string Selection { get; set; } = "#4A4A4A";
    public string Caret { get; set; } = "#FFFFFF";
    public string CurrentLine { get; set; } = "#252525";
    public string LineNumbers { get; set; } = "#7A7A7A";
    public string MinimapBg { get; set; } = "#181818";
    public string Chrome { get; set; } = "#121212";
    public string ActiveTab { get; set; } = "#383838";
    public string Hover { get; set; } = "#2E2E2E";
    public string Accent { get; set; } = "#D0D0D0";

    public ThemeColors Clone() => (ThemeColors)MemberwiseClone();

    public static Color C(string hex)
    {
        try { return Color.Parse(hex); } catch { return Colors.Gray; }
    }

    public static SolidColorBrush B(string hex) => new SolidColorBrush(C(hex));

    public static bool IsDark(string hex)
    {
        var c = C(hex);
        return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) < 140;
    }

    public static readonly List<ThemeColors> BuiltIn = new()
    {
        new ThemeColors(),
        new ThemeColors { Name="Light", Background="#FFFFFF", Text="#1F1F1F", Selection="#BBD6F5", Caret="#000000", CurrentLine="#F3F3F3", LineNumbers="#9A9A9A", MinimapBg="#F7F7F7", Chrome="#EDEDED", ActiveTab="#FFFFFF", Hover="#DCDCDC", Accent="#404040" },
        new ThemeColors { Name="Midnight", Background="#0B0F1A", Text="#D6DEF0", Selection="#24365E", Caret="#FFFFFF", CurrentLine="#111729", LineNumbers="#4C5878", MinimapBg="#090C15", Chrome="#070A12", ActiveTab="#1B2540", Hover="#1A2238", Accent="#5B8CFF" },
        new ThemeColors { Name="Nord", Background="#2E3440", Text="#D8DEE9", Selection="#434C5E", Caret="#D8DEE9", CurrentLine="#3B4252", LineNumbers="#616E88", MinimapBg="#2A303B", Chrome="#272C36", ActiveTab="#434C5E", Hover="#3B4252", Accent="#88C0D0" },
        new ThemeColors { Name="Dracula", Background="#282A36", Text="#F8F8F2", Selection="#44475A", Caret="#F8F8F0", CurrentLine="#303243", LineNumbers="#6272A4", MinimapBg="#232530", Chrome="#1E1F29", ActiveTab="#3A3C4E", Hover="#343746", Accent="#BD93F9" },
        new ThemeColors { Name="Monokai", Background="#272822", Text="#F8F8F2", Selection="#49483E", Caret="#F8F8F0", CurrentLine="#31322B", LineNumbers="#75715E", MinimapBg="#22231E", Chrome="#1D1E19", ActiveTab="#3E3D32", Hover="#3A3B33", Accent="#A6E22E" },
        new ThemeColors { Name="Solarized", Background="#002B36", Text="#93A1A1", Selection="#0B4A5A", Caret="#EEE8D5", CurrentLine="#073642", LineNumbers="#586E75", MinimapBg="#00252E", Chrome="#001F27", ActiveTab="#0E4452", Hover="#0A3A47", Accent="#2AA198" },
    };
}

public class AppSettings
{
    public ThemeColors Colors { get; set; } = new();
    public List<ThemeColors> CustomThemes { get; set; } = new();
    public string FontFamily { get; set; } = "Cascadia Code";
    public double FontSize { get; set; } = 14;
    public bool Bold { get; set; } = false;
    public int TabSize { get; set; } = 4;
    public int UiScale { get; set; } = 100;
    public bool WordWrap { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = false;
    public bool ShowMinimap { get; set; } = false;
    public int MinimapWidth { get; set; } = 110;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowWhitespace { get; set; } = false;
    public bool HighlightCurrentLine { get; set; } = true;
    public string DefaultEncoding { get; set; } = "UTF-8";
    public string DefaultLineEnding { get; set; } = OperatingSystem.IsWindows() ? "CRLF" : "LF";
    public List<string> Recent { get; set; } = new();
    public bool RestoreSession { get; set; } = true;
    public bool BackupOnSave { get; set; } = false;
    // clean-up when saving (on by default; every part can be switched off in Settings > Files and saving)
    public bool RememberWindowSize { get; set; } = true;
    public int WinWidth { get; set; } = 0;                  // size of the main window when it was last closed
    public int WinHeight { get; set; } = 0;
    public bool WinMaximized { get; set; } = false;
    public bool CleanOnSave { get; set; } = true;
    public bool CleanTrailing { get; set; } = true;
    public bool CleanBlankLines { get; set; } = true;
    public bool CleanDoubleSpaces { get; set; } = false;
    public bool CleanTabsToSpaces { get; set; } = false;
    public Dictionary<string, int> FilePositions { get; set; } = new();      // last caret position of each file
    public bool AutoReload { get; set; } = true;
    public bool ShowSidebar { get; set; } = false;
    public Dictionary<string, string> KeyMap { get; set; } = new();   // only the shortcuts the user changed
    public string LastFolder { get; set; } = "";

    // ---- static ----
    public const string FontFallback = "Cascadia Code, Consolas, Menlo, DejaVu Sans Mono, Liberation Mono, monospace";
    public static AppSettings Current { get; private set; } = new();
    public static event Action Changed;
    public static string DataDir { get; private set; }
    public static bool DataWritable { get; private set; } = true;

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    /// <summary>
    /// The portable "data" folder lives in the Notium folder, shared by all operating systems.
    /// The start-* launchers set NOTIUM_HOME to that folder; without it the folder next to the program is used.
    /// </summary>
    static string ResolveDataDir()
    {
        string home = Environment.GetEnvironmentVariable("NOTIUM_HOME");
        if (!string.IsNullOrWhiteSpace(home)) return Path.Combine(home, "data");
        return Path.Combine(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "data");
    }

    public static void Init()
    {
        DataDir = ResolveDataDir();
        try
        {
            Directory.CreateDirectory(DataDir);
            string probe = Path.Combine(DataDir, ".w");
            File.WriteAllText(probe, "x"); File.Delete(probe);
        }
        catch { DataWritable = false; }
        try
        {
            string p = Path.Combine(DataDir, "settings.json");
            if (File.Exists(p)) Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(p)) ?? new();
        }
        catch { Current = new(); }
        Current.ShowSidebar = false;      // the folder panel always starts closed
    }

    public static void Save()
    {
        if (!DataWritable) return;
        try { File.WriteAllText(Path.Combine(DataDir, "settings.json"), JsonSerializer.Serialize(Current, Opt)); } catch { }
    }

    public static void Notify() { Save(); Changed?.Invoke(); }

    public static void Reset() { Current = new(); Notify(); }
}
}

// ---- Keymap.cs
namespace Notium
{
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

public record CommandInfo(string Id, string Name, string Default);

/// <summary>Keyboard shortcuts: the defaults, the user's changes (saved in settings) and the key matching.
/// A shortcut is written like "Ctrl+Shift+T" (Cmd counts as Ctrl on macOS).</summary>
public static class Keymap
{
    public static readonly CommandInfo[] All =
    {
        new("new-tab", "New tab", "Ctrl+T"),
        new("new-tab-alt", "New tab (second shortcut)", "Ctrl+N"),
        new("new-window", "New window", "Ctrl+Shift+N"),
        new("open", "Open file", "Ctrl+O"),
        new("save", "Save", "Ctrl+S"),
        new("save-as", "Save as", "Ctrl+Shift+S"),
        new("print", "Print (preview)", "Ctrl+P"),
        new("close-tab", "Close tab", "Ctrl+W"),
        new("reopen-tab", "Reopen closed tab", "Ctrl+Shift+T"),
        new("pin-tab", "Pin / unpin tab", "Ctrl+Shift+P"),
        new("next-tab", "Next tab", "Ctrl+Tab"),
        new("prev-tab", "Previous tab", "Ctrl+Shift+Tab"),
        new("find", "Find", "Ctrl+F"),
        new("replace", "Replace", "Ctrl+H"),
        new("find-next", "Next result", "F3"),
        new("find-prev", "Previous result", "Shift+F3"),
        new("find-in-files", "Find in files", "Ctrl+Shift+F"),
        new("goto-line", "Go to line", "Ctrl+G"),
        new("bookmark", "Toggle bookmark", "Ctrl+F2"),
        new("bookmark-next", "Next bookmark", "F2"),
        new("bookmark-prev", "Previous bookmark", "Shift+F2"),
        new("move-up", "Move line up", "Alt+Up"),
        new("move-down", "Move line down", "Alt+Down"),
        new("comment", "Toggle comment", "Ctrl+/"),
        new("delete-line", "Delete line", "Ctrl+D"),
        new("duplicate-line", "Duplicate line", "Ctrl+Shift+D"),
        new("insert-datetime", "Insert date and time", "F5"),
        new("sidebar", "Folder panel", "Ctrl+B"),
        new("zoom-in", "Zoom in", "Ctrl+="),
        new("zoom-out", "Zoom out", "Ctrl+-"),
        new("zoom-reset", "Reset zoom", "Ctrl+0"),
        new("zen", "Distraction-free mode", "F11"),
        new("settings", "Settings", "Ctrl+,"),
    };

    public static CommandInfo Info(string id) => All.First(c => c.Id == id);

    /// <summary>The shortcut now in use for a command ("" = none).</summary>
    public static string Get(string id)
        => AppSettings.Current.KeyMap.TryGetValue(id, out var v) ? v : Info(id).Default;

    public static bool IsCustom(string id) => AppSettings.Current.KeyMap.ContainsKey(id);

    public static void Set(string id, string shortcut)
    {
        if (shortcut == Info(id).Default) AppSettings.Current.KeyMap.Remove(id);
        else AppSettings.Current.KeyMap[id] = shortcut;
    }

    public static void Reset(string id) => AppSettings.Current.KeyMap.Remove(id);
    public static void ResetAll() => AppSettings.Current.KeyMap.Clear();

    public static string FindId(string shortcut, string except = null)
    {
        foreach (var c in All)
            if (c.Id != except && Get(c.Id) == shortcut) return c.Id;
        return null;
    }

    public static string Display(string shortcut) => string.IsNullOrEmpty(shortcut) ? "Not set" : shortcut;

    /// <summary>A shortcut must use Ctrl or Alt (otherwise it would break normal typing); function keys can stand alone.</summary>
    public static bool IsAllowed(string shortcut)
    {
        var parts = shortcut.Split('+');
        if (parts.Contains("Ctrl") || parts.Contains("Alt")) return true;
        return Regex.IsMatch(parts[^1], @"^F\d{1,2}$");
    }

    static string KeyName(Key k)
    {
        switch (k)
        {
            case Key.None: case Key.LeftCtrl: case Key.RightCtrl: case Key.LeftShift: case Key.RightShift:
            case Key.LeftAlt: case Key.RightAlt: case Key.LWin: case Key.RWin: case Key.CapsLock:
                return null;
            case Key.OemPlus: case Key.Add: return "=";
            case Key.OemMinus: case Key.Subtract: return "-";
            case Key.Oem2: case Key.Divide: return "/";
            case Key.OemComma: return ",";
            case Key.OemPeriod: return ".";
        }
        if (k >= Key.D0 && k <= Key.D9) return ((int)(k - Key.D0)).ToString();
        if (k >= Key.NumPad0 && k <= Key.NumPad9) return ((int)(k - Key.NumPad0)).ToString();
        string n = k.ToString();
        return n.StartsWith("Oem") ? null : n;
    }

    /// <summary>The shortcut text of a key press, or null for a key that cannot be a shortcut.</summary>
    public static string FromEvent(KeyEventArgs e)
    {
        string key = KeyName(e.Key);
        if (key == null) return null;
        var m = e.KeyModifiers;
        bool ctrl = (m & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        bool alt = (m & KeyModifiers.Alt) != 0;
        bool shift = (m & KeyModifiers.Shift) != 0;
        if (key == "=") shift = false;              // Ctrl++ and Ctrl+= are the same shortcut
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        parts.Add(key);
        return string.Join("+", parts);
    }

    /// <summary>The shortcut as a KeyGesture, only to show it next to a menu item.</summary>
    public static KeyGesture Gesture(string id)
    {
        string g = Get(id);
        if (string.IsNullOrEmpty(g)) return null;
        try
        {
            var parts = g.Split('+');
            var mods = KeyModifiers.None;
            foreach (var p in parts.Take(parts.Length - 1))
            {
                if (p == "Ctrl") mods |= KeyModifiers.Control;
                else if (p == "Alt") mods |= KeyModifiers.Alt;
                else if (p == "Shift") mods |= KeyModifiers.Shift;
            }
            string n = parts[^1];
            Key key = n switch
            {
                "=" => Key.OemPlus,
                "-" => Key.OemMinus,
                "/" => Key.Oem2,
                "," => Key.OemComma,
                "." => Key.OemPeriod,
                _ when n.Length == 1 && char.IsDigit(n[0]) => Enum.Parse<Key>("D" + n),
                _ => Enum.Parse<Key>(n)
            };
            return new KeyGesture(key, mods);
        }
        catch { return null; }
    }
}

/// <summary>Small window that waits for the new key combination.</summary>
public class ShortcutDialog : DialogBase
{
    readonly TextBlock _msg;

    ShortcutDialog(string name) : base("Change shortcut", 440)
    {
        var sp = new StackPanel { Margin = new Thickness(24, 22, 24, 20) };
        sp.Children.Add(new TextBlock { Text = $"New shortcut for \"{name}\"", FontSize = 15, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        _msg = new TextBlock { Text = "Press the key combination now...", Margin = new Thickness(0, 14, 0, 0), Opacity = 0.8, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        sp.Children.Add(_msg);
        var bar = ButtonBar();
        var none = new Button { Content = "Remove shortcut", HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        none.Click += (s, e) => Close("");
        cancel.Click += (s, e) => Close((string)null);
        bar.Children.Add(none); bar.Children.Add(cancel);
        sp.Children.Add(bar);
        Content = sp;

        AddHandler(KeyDownEvent, (sender, ev) =>
        {
            ev.Handled = true;
            if (ev.Key == Key.Escape) { Close((string)null); return; }
            string g = Keymap.FromEvent(ev);
            if (g == null) return;                  // only a modifier so far
            if (!Keymap.IsAllowed(g)) { _msg.Text = $"\"{g}\" cannot be used alone. Add Ctrl or Alt."; return; }
            Close(g);
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>Returns the shortcut, "" to remove it, or null if cancelled.</summary>
    public static Task<string> Capture(Window owner, string name)
        => new ShortcutDialog(name).ShowDialog<string>(owner);
}
}

// ---- Session.cs
namespace Notium
{
using System.Text.Json;

/// <summary>Optional "restore my tabs" feature (off by default). Stored in data/session.json.</summary>
public class SessionTab
{
    public string Path { get; set; }
    public string Title { get; set; }
    public string Text { get; set; }          // only for untitled or modified documents
    public bool Dirty { get; set; }
    public string Language { get; set; }
    public string Encoding { get; set; }
    public string Eol { get; set; }
    public bool Pinned { get; set; }
}

public static class Session
{
    static string FilePath => System.IO.Path.Combine(AppSettings.DataDir, "session.json");

    public static void Save(IEnumerable<TabDoc> tabs)
    {
        if (!AppSettings.DataWritable) return;
        try
        {
            var list = new List<SessionTab>();
            foreach (var t in tabs)
            {
                if (t.Path == null && t.Doc.TextLength == 0) continue;
                list.Add(new SessionTab
                {
                    Path = t.Path, Title = t.Title, Dirty = t.Dirty, Language = t.Language, Encoding = t.Encoding, Eol = t.Eol, Pinned = t.Pinned,
                    Text = t.Path == null || t.Dirty ? t.Doc.Text : null
                });
            }
            if (list.Count == 0) { Clear(); return; }
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }

    public static List<TabDoc> Load()
    {
        var res = new List<TabDoc>();
        try
        {
            if (!File.Exists(FilePath)) return res;
            var list = JsonSerializer.Deserialize<List<SessionTab>>(File.ReadAllText(FilePath)) ?? new();
            foreach (var st in list)
            {
                try
                {
                    if (st.Text == null)
                    {
                        if (st.Path != null && File.Exists(st.Path)) { var ft = TabDoc.FromFile(st.Path); ft.Pinned = st.Pinned; res.Add(ft); }
                        continue;
                    }
                    var t = new TabDoc
                    {
                        Path = st.Path, Title = st.Title ?? "Untitled", Language = st.Language ?? Syntax.Plain,
                        Encoding = st.Encoding ?? AppSettings.Current.DefaultEncoding, Eol = st.Eol ?? AppSettings.Current.DefaultLineEnding
                    };
                    t.SetText(st.Text);
                    t.Dirty = st.Dirty; t.Pinned = st.Pinned;
                    if (st.Path != null && File.Exists(st.Path)) t.DiskTime = File.GetLastWriteTimeUtc(st.Path);
                    res.Add(t);
                }
                catch { }
            }
        }
        catch { }
        return res;
    }
}
}

// ---- Syntax.cs
namespace Notium
{
using System.Text.RegularExpressions;
using TextMateSharp.Grammars;

/// <summary>Maps file names to programming languages (TextMate grammars) and knows how to comment each one.</summary>
public static class Syntax
{
    public const string Plain = "Plain text";

    static readonly Dictionary<string, string> ByExt = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dockerfile"] = "dockerfile", ["makefile"] = "makefile", ["gnumakefile"] = "makefile", ["cmakelists.txt"] = "cmake",
        [".bashrc"] = "shellscript", [".zshrc"] = "shellscript", [".profile"] = "shellscript",
        [".gitignore"] = "ignore", [".env"] = "dotenv", ["gemfile"] = "ruby", ["rakefile"] = "ruby",
    };
    static readonly Dictionary<string, string> Names = new();
    static readonly List<string> Ids = new();
    static readonly Dictionary<string, string> ExtById = new()
    {
        ["python"] = "py", ["javascript"] = "js", ["typescript"] = "ts", ["csharp"] = "cs", ["cpp"] = "cpp", ["c"] = "c",
        ["java"] = "java", ["go"] = "go", ["rust"] = "rs", ["ruby"] = "rb", ["php"] = "php", ["html"] = "html", ["css"] = "css",
        ["json"] = "json", ["xml"] = "xml", ["yaml"] = "yaml", ["markdown"] = "md", ["sql"] = "sql", ["shellscript"] = "sh",
        ["powershell"] = "ps1", ["bat"] = "bat", ["kotlin"] = "kt", ["swift"] = "swift", ["lua"] = "lua", ["perl"] = "pl",
        ["r"] = "r", ["dart"] = "dart", ["scss"] = "scss", ["less"] = "less", ["dockerfile"] = "dockerfile", ["makefile"] = "mk",
        ["objective-c"] = "m", ["fsharp"] = "fs", ["vb"] = "vb", ["groovy"] = "groovy", ["scala"] = "scala", ["toml"] = "toml",
    };
    static bool _ready;

    static void Init()
    {
        if (_ready) return;
        _ready = true;
        try
        {
            var reg = new RegistryOptions(ThemeName.DarkPlus);
            foreach (var l in reg.GetAvailableLanguages())
            {
                if (l.Id == "plaintext") continue;
                Ids.Add(l.Id);
                string alias = l.Aliases != null && l.Aliases.Count > 0 ? l.Aliases[0] : l.Id;
                Names[l.Id] = alias;
                if (l.Extensions != null && l.Extensions.Count > 0 && !ExtById.ContainsKey(l.Id)) ExtById[l.Id] = l.Extensions[0].TrimStart('.');
                if (l.Extensions != null)
                    foreach (var e in l.Extensions) if (!ByExt.ContainsKey(e)) ByExt[e] = l.Id;
            }
            Ids.Sort((a, b) => string.Compare(Display(a), Display(b), StringComparison.CurrentCultureIgnoreCase));
        }
        catch { }
    }

    /// <summary>The language id for a file name, chosen by the extension typed in the name (".txt" and unknown ones are plain text).</summary>
    public static string ForPath(string path)
    {
        Init();
        if (string.IsNullOrEmpty(path)) return Plain;
        string name = Path.GetFileName(path);
        if (ByName.TryGetValue(name, out var n) && (Ids.Contains(n) || ByExt.ContainsValue(n))) return n;
        string ext = Path.GetExtension(path);
        if (ext.Length > 0 && !ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) && ByExt.TryGetValue(ext, out var id)) return id;
        return Plain;
    }

    /// <summary>The usual file extension of a language, without the dot ("txt" for plain text).</summary>
    public static string DefaultExt(string id)
    {
        Init();
        return id != null && ExtById.TryGetValue(id, out var e) && !string.IsNullOrEmpty(e) ? e : "txt";
    }

    /// <summary>Guesses the language of some code from its content ("Plain text" when it is not sure).</summary>
    public static string Guess(string text)
    {
        Init();
        if (string.IsNullOrWhiteSpace(text)) return Plain;
        string t = text.TrimStart();
        if (t.Length < 6) return Plain;
        string R(string id) => Ids.Contains(id) ? id : Plain;

        if (t.StartsWith("<?php")) return R("php");
        if (t.StartsWith("#!"))
        {
            string first = t.Split('\n')[0];
            if (first.Contains("python")) return R("python");
            if (first.Contains("node")) return R("javascript");
            if (first.Contains("ruby")) return R("ruby");
            if (first.Contains("perl")) return R("perl");
            if (first.Contains("pwsh") || first.Contains("powershell")) return R("powershell");
            return R("shellscript");
        }
        if (t.StartsWith("<?xml")) return R("xml");
        if (Regex.IsMatch(t, @"^(<!DOCTYPE\s+html|<html)", RegexOptions.IgnoreCase)) return R("html");
        if (t[0] == '{' || t[0] == '[')
        {
            try { using var json = System.Text.Json.JsonDocument.Parse(t); return R("json"); } catch { }
        }

        var sc = new Dictionary<string, double>();
        void Add(string id, double v) { if (v > 0) sc[id] = (sc.TryGetValue(id, out var o) ? o : 0) + v; }
        int Count(string pat, RegexOptions o = RegexOptions.Multiline) => Regex.Matches(text, pat, o).Count;
        int Min(int a, int b) => Math.Min(a, b);

        // Python
        Add("python", 3 * Min(Count(@"^\s*def\s+\w+\s*\(.*\)\s*(->.*)?:\s*$"), 2));
        Add("python", 3 * Min(Count(@"^\s*from\s+[\w.]+\s+import\s+\S"), 1));
        Add("python", 2 * Min(Count(@"^\s*import\s+[\w.]+(\s+as\s+\w+)?\s*$"), 1));
        Add("python", 2 * Min(Count(@"^\s*(if|elif|else|for|while|try|except|finally|with|class)\b[^\n{};]*:\s*$"), 2));
        Add("python", Min(Count(@"\bprint\("), 2) + Min(Count(@"\bself\."), 2) + 2 * Min(Count(@"^\s*if\s+__name__\s*=="), 1));
        // JavaScript / TypeScript
        double js = 2 * Min(Count(@"\b(const|let|var)\s+\w+\s*="), 2) + 2 * Min(Count(@"\bfunction\s*\w*\s*\("), 2) + Min(Count(@"=>"), 2)
                  + 2 * Min(Count(@"\bconsole\.\w+\("), 1) + 2 * Min(Count(@"\brequire\("), 1) + 2 * Min(Count(@"^\s*import\s+.+\s+from\s+['""]"), 1)
                  + 0.5 * Min(Count(@";\s*$"), 4);
        double ts = 3 * Min(Count(@"^\s*(export\s+)?(interface|type)\s+\w+"), 1) + 2 * Min(Count(@":\s*(string|number|boolean|any|void)\b"), 2);
        if (ts >= 2 && js >= 2) Add("typescript", js + ts); else Add("javascript", js);
        // Java / C#
        Add("java", 2 * Min(Count(@"\bpublic\s+(static\s+)?(class|void|int)\b"), 1) + 3 * Min(Count(@"System\.out\."), 1)
                  + 3 * Min(Count(@"^\s*import\s+java\."), 1) + 2 * Min(Count(@"String\[\]\s+args"), 1));
        Add("csharp", 3 * Min(Count(@"^\s*using\s+System"), 1) + 2 * Min(Count(@"^\s*namespace\s+[\w.]+"), 1)
                    + 3 * Min(Count(@"\bConsole\.(Write|Read)"), 1) + Min(Count(@"\bstring\s+\w+\s*="), 1));
        // C / C++
        int inc = Count(@"^\s*#include\s*[<""]");
        if (inc > 0 || Count(@"\bint\s+main\s*\(") > 0)
        {
            bool cpp = Count(@"std::|\bcout\b|\bclass\s+\w+|template\s*<|^\s*using\s+namespace") > 0;
            Add(cpp ? "cpp" : "c", 3 * Min(inc, 1) + 3 * Min(Count(@"\bint\s+main\s*\("), 1));
        }
        // Go / Rust
        Add("go", 3 * Min(Count(@"^\s*package\s+\w+\s*$"), 1) + 2 * Min(Count(@"\bfunc\s+\w+\s*\("), 1) + 3 * Min(Count(@"\bfmt\."), 1) + Min(Count(@":="), 1));
        Add("rust", 3 * Min(Count(@"\bfn\s+\w+\s*[(<]"), 1) + 2 * Min(Count(@"\blet\s+mut\s"), 1) + 3 * Min(Count(@"\bprintln!"), 1) + 3 * Min(Count(@"^\s*use\s+std::"), 1));
        // Ruby / Lua
        Add("ruby", 2 * Min(Count(@"^\s*def\s+\w+[^:\n]*$"), 1) + 2 * Min(Count(@"^\s*end\s*$"), 1) + 2 * Min(Count(@"^\s*puts\s"), 1) + Min(Count(@"^\s*require\s+['""]"), 1));
        Add("lua", 2 * Min(Count(@"^\s*local\s+\w+"), 1) + 2 * Min(Count(@"\bthen\s*$"), 1) + Min(Count(@"^\s*end\s*$"), 1));
        // Kotlin / Swift
        Add("kotlin", 3 * Min(Count(@"\bfun\s+\w+\s*\("), 1) + Min(Count(@"\b(val|var)\s+\w+\s*[:=]"), 1));
        Add("swift", 3 * Min(Count(@"^\s*import\s+(Foundation|UIKit|SwiftUI)"), 1) + 2 * Min(Count(@"\bfunc\s+\w+\s*\(.*\)\s*(->|\{)"), 1) + Min(Count(@"\b(let|var)\s+\w+\s*[:=]"), 1));
        // SQL
        Add("sql", 4 * Min(Count(@"\bselect\b.+\bfrom\b", RegexOptions.IgnoreCase | RegexOptions.Singleline), 1)
                 + 4 * Min(Count(@"^\s*(insert\s+into|create\s+table|update\s+\w+\s+set|delete\s+from)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline), 1));
        // CSS
        if (Count(@"^\s*([.#@*:][\w\-.#:>, ]*|(body|html|div|span|a|p|h[1-6]|ul|li|table|input|button|img|header|footer|nav|main|section)\b[\w\-.#:>, ]*)\{\s*$") > 0
            && Count(@"^\s*[\w-]+\s*:\s*[^;{}\n]+;\s*$") > 0) Add("css", 4);
        // Markdown
        int head = Count(@"^#{1,6}\s+\S"), fence = Count(@"^```");
        if (head > 0 || fence > 0)
            Add("markdown", 2 * Min(head, 2) + 3 * Min(fence, 1) + 0.7 * Min(Count(@"^\s*[-*]\s+\S"), 3) + 2 * Min(Count(@"\[[^\]]+\]\([^)]+\)"), 1));
        // shell, PowerShell, batch
        Add("shellscript", 1.5 * Min(Count(@"^\s*(echo|cd|ls|grep|sudo|apt|apt-get|export|chmod|mkdir)\s"), 3) + Min(Count(@"\$\{?\w+"), 1) + 2 * Min(Count(@"^\s*(fi|done)\s*$"), 1));
        Add("powershell", 3 * Min(Count(@"\b(Get|Set|New|Write|Remove)-\w+", RegexOptions.Multiline), 1) + 2 * Min(Count(@"\$\w+\s*="), 1));
        Add("bat", 4 * Min(Count(@"^@?echo\s+(off|on)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline), 1) + Min(Count(@"^\s*(set|goto|rem)\s", RegexOptions.IgnoreCase | RegexOptions.Multiline), 1));

        string best = null; double bestMargin = 0;
        foreach (var kv in sc)
        {
            double need = kv.Key == "python" ? 2 : kv.Key == "markdown" || kv.Key == "sql" || kv.Key == "powershell" ? 4 : 3;
            if (kv.Value >= need && kv.Value - need >= bestMargin) { best = kv.Key; bestMargin = kv.Value - need; }
        }
        return best != null ? R(best) : Plain;
    }

    public static List<string> AllIds() { Init(); return Ids; }

    public static string Display(string id)
    {
        Init();
        if (id == Plain || string.IsNullOrEmpty(id)) return Plain;
        return Names.TryGetValue(id, out var n) ? n : id;
    }

    static readonly HashSet<string> Hash = new()
    {
        "python", "ruby", "shellscript", "bash", "perl", "perl6", "yaml", "powershell", "r", "makefile", "dockerfile", "toml",
        "coffeescript", "elixir", "julia", "nim", "cmake", "dotenv", "ignore", "properties", "gdscript", "raku", "crystal"
    };
    static readonly HashSet<string> Dash = new() { "lua", "sql", "haskell", "elm", "ada", "vhdl", "plsql" };
    static readonly HashSet<string> Semi = new() { "ini", "clojure", "lisp", "scheme", "asm", "nasm", "racket" };
    static readonly HashSet<string> Percent = new() { "latex", "tex", "matlab", "erlang", "prolog" };
    static readonly HashSet<string> Block = new() { "html", "xml", "xsl", "markdown", "svg", "vue", "razor", "handlebars" };
    static readonly HashSet<string> Quote = new() { "vb", "vba", "vbscript" };

    /// <summary>Comment markers for a language: a line prefix, plus a closing marker for languages that only have block comments.</summary>
    public static (string open, string close) Comment(string id)
    {
        if (id == null || id == Plain) return ("#", "");
        if (Block.Contains(id)) return ("<!--", "-->");
        if (Hash.Contains(id)) return ("#", "");
        if (Dash.Contains(id)) return ("--", "");
        if (Semi.Contains(id)) return (";", "");
        if (Percent.Contains(id)) return ("%", "");
        if (Quote.Contains(id)) return ("'", "");
        if (id == "bat") return ("REM", "");
        if (id == "css" || id == "scss" || id == "less") return ("/*", "*/");
        return ("//", "");
    }
}
}

// ---- Highlight.cs
namespace Notium
{
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

/// <summary>Syntax coloring for every programming language, using TextMate grammars (the same ones as VS Code).</summary>
public sealed class Highlighter
{
    readonly TextEditor _ed;
    RegistryOptions _reg;
    TextMate.Installation _tm;
    bool _dark;

    public Highlighter(TextEditor ed) { _ed = ed; }

    /// <summary>Selects the color scheme (dark/light) and the grammar of the language. Installing or changing the scheme
    /// recolors the editor, so the caller must apply its own theme colors afterwards.</summary>
    public void Apply(bool dark, string languageId)
    {
        try
        {
            var theme = dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
            if (_tm == null)
            {
                _reg = new RegistryOptions(theme);
                _tm = _ed.InstallTextMate(_reg);
                _dark = dark;
            }
            else if (_dark != dark)
            {
                _tm.SetTheme(_reg.LoadTheme(theme));
                _dark = dark;
            }
            string scope = string.IsNullOrEmpty(languageId) || languageId == Syntax.Plain ? null : _reg.GetScopeByLanguageId(languageId);
            _tm.SetGrammar(scope);
        }
        catch { /* the text stays uncolored; the editor keeps working */ }
    }
}
}

// ---- Documents.cs
namespace Notium
{
using System.IO;
using System.Text;
using AvaloniaEdit.Document;

public static class Enc
{
    public static readonly string[] Names = { "UTF-8", "UTF-8 with BOM", "UTF-16 LE", "UTF-16 BE", "ASCII", "Windows-1252" };

    public static Encoding Get(string name) => name switch
    {
        "UTF-8 with BOM" => new UTF8Encoding(true),
        "UTF-16 LE" => new UnicodeEncoding(false, true),
        "UTF-16 BE" => new UnicodeEncoding(true, true),
        "ASCII" => Encoding.ASCII,
        "Windows-1252" => Encoding.GetEncoding(1252),
        _ => new UTF8Encoding(false),
    };

    /// <summary>Detects the encoding and decodes the text.</summary>
    public static string Decode(byte[] b, out string encName)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
        { encName = "UTF-8 with BOM"; return Encoding.UTF8.GetString(b, 3, b.Length - 3); }
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
        { encName = "UTF-16 LE"; return Encoding.Unicode.GetString(b, 2, b.Length - 2); }
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
        { encName = "UTF-16 BE"; return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2); }
        try
        {
            string s = new UTF8Encoding(false, true).GetString(b);
            encName = "UTF-8"; return s;
        }
        catch
        {
            encName = "Windows-1252"; return Encoding.GetEncoding(1252).GetString(b);
        }
    }

    /// <summary>Decodes the bytes with a chosen encoding (used by "Reopen with encoding").</summary>
    public static string DecodeAs(byte[] b, string name)
    {
        var enc = Get(name);
        int skip = 0;
        var pre = enc.GetPreamble();
        if (pre.Length > 0 && b.Length >= pre.Length && b.Take(pre.Length).SequenceEqual(pre)) skip = pre.Length;
        else if (name == "UTF-8" && b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) skip = 3;
        return enc.GetString(b, skip, b.Length - skip);
    }

    public static string DetectEol(string s)
    {
        int crlf = 0, lf = 0, cr = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\r') { if (i + 1 < s.Length && s[i + 1] == '\n') { crlf++; i++; } else cr++; }
            else if (s[i] == '\n') lf++;
        }
        if (crlf == 0 && lf == 0 && cr == 0) return AppSettings.Current.DefaultLineEnding;
        if (crlf >= lf && crlf >= cr) return "CRLF";
        return lf >= cr ? "LF" : "CR";
    }

    public static string EolString(string e) => e == "LF" ? "\n" : e == "CR" ? "\r" : "\r\n";

    public static string Normalize(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');
}

public class TabDoc
{
    public TextDocument Doc { get; } = new();
    public string Path { get; set; }
    public string Title { get; set; } = "Untitled";
    public string Encoding { get; set; } = AppSettings.Current.DefaultEncoding;
    public string Eol { get; set; } = AppSettings.Current.DefaultLineEnding;
    public string Language { get; set; } = Syntax.Plain;
    public bool Pinned;
    public List<AvaloniaEdit.Document.TextAnchor> Bookmarks = new();
    public int SavedOffset = -1;       // caret position to come back to
    public bool PosLoaded;                // pinned tabs stay at the left and cannot be closed by accident
    public bool LangManual;            // the user picked the language: stop guessing it from the text
    public bool Dirty { get; set; }
    public DateTime DiskTime { get; set; }
    public bool Loading;

    /// <summary>Like Windows Notepad: an untitled, empty document does not ask to be saved.</summary>
    public bool NeedsSave => Path == null ? Doc.TextLength > 0 : Dirty;

    public TabDoc()
    {
        Doc.TextChanged += (s, e) => { if (!Loading) { Dirty = true; Changed?.Invoke(this); } };
    }

    public event Action<TabDoc> Changed;
    public void RaiseChanged() => Changed?.Invoke(this);

    public void SetText(string normalized)
    {
        Loading = true; Doc.Text = normalized; Doc.UndoStack.ClearAll(); Loading = false;
    }

    /// <summary>The result of reading a file; it can be produced on a background thread.</summary>
    public record LoadedFile(string Path, string Text, string Encoding, string Eol, DateTime DiskTime);

    public static LoadedFile Read(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        string s = Enc.Decode(b, out string en);
        return new LoadedFile(path, Enc.Normalize(s), en, Enc.DetectEol(s), File.GetLastWriteTimeUtc(path));
    }

    public static TabDoc FromLoaded(LoadedFile f)
    {
        var t = new TabDoc { Path = f.Path, Title = System.IO.Path.GetFileName(f.Path), Encoding = f.Encoding, Eol = f.Eol };
        t.SetText(f.Text);
        t.DiskTime = f.DiskTime;
        t.Dirty = false;
        t.Language = Syntax.ForPath(f.Path);
        return t;
    }

    public static TabDoc FromFile(string path) => FromLoaded(Read(path));

    public void Save(string path = null)
    {
        path ??= Path;
        string text = Enc.Normalize(Doc.Text).Replace("\n", Enc.EolString(Eol));
        var enc = Enc.Get(Encoding);
        byte[] body = enc.GetBytes(text);
        byte[] pre = enc.GetPreamble();
        string tmp = path + ".notium.tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            fs.Write(pre, 0, pre.Length); fs.Write(body, 0, body.Length);
        }
        if (AppSettings.Current.BackupOnSave && File.Exists(path))
        {
            try { File.Copy(path, path + ".bak", true); } catch { }
        }
        File.Move(tmp, path, true);
        string oldExt = Path == null ? "" : System.IO.Path.GetExtension(Path);
        string newExt = System.IO.Path.GetExtension(path);
        Path = path; Title = System.IO.Path.GetFileName(path);
        DiskTime = File.GetLastWriteTimeUtc(path);
        Dirty = false;
        // the extension typed in the file name decides the programming language
        if (Language == Syntax.Plain || !string.Equals(oldExt, newExt, StringComparison.OrdinalIgnoreCase))
            Language = Syntax.ForPath(path);
    }
}
}

// ---- Dialogs.cs
namespace Notium
{
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

/// <summary>Common look of the small dialogs (themed, centered on the owner).</summary>
public abstract class DialogBase : Window
{
    protected DialogBase(string title, double width)
    {
        Title = title; Width = width; SizeToContent = SizeToContent.Height; CanResize = false;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Opened += (s, e) => { if (Content is Control root) Fx.FadeIn(root, 130); };
        var c = AppSettings.Current.Colors;
        Background = ThemeColors.B(c.Background);
        Foreground = ThemeColors.B(c.Text);
    }

    protected static StackPanel ButtonBar()
        => new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 20, 0, 0) };
}

/// <summary>Message box with custom buttons. The first button is the default, the last one cancels.</summary>
public class MessageDialog : DialogBase
{
    MessageDialog(string text, string[] buttons) : base("Notium", 440)
    {
        var sp = new StackPanel { Margin = new Thickness(24, 22, 24, 20) };
        sp.Children.Add(new TextBlock { Text = text, FontSize = 15, TextWrapping = TextWrapping.Wrap });
        var bar = ButtonBar();
        for (int i = 0; i < buttons.Length; i++)
        {
            int idx = i;
            var b = new Button { Content = buttons[i], MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = i == 0, IsCancel = buttons.Length > 1 && i == buttons.Length - 1 };
            if (buttons[i] == "Don't save") b.Classes.Add("danger");      // turns red under the mouse
            b.Click += (s, e) => Close((int?)idx);
            bar.Children.Add(b);
        }
        sp.Children.Add(bar);
        Content = sp;
    }

    /// <summary>Returns the index of the pressed button, or -1 if the dialog was closed.</summary>
    public static async Task<int> Show(Window owner, string text, params string[] buttons)
    {
        if (buttons.Length == 0) buttons = new[] { "OK" };
        var w = new MessageDialog(text, buttons);
        int? r = await w.ShowDialog<int?>(owner);
        return r ?? -1;
    }

    public static Task Error(Window owner, string text) => Show(owner, text, "OK");
}

public class InputDialog : DialogBase
{
    readonly TextBox _tb;

    InputDialog(string title, string prompt, string def) : base(title, 400)
    {
        var sp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        sp.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });
        _tb = new TextBox { Text = def };
        sp.Children.Add(_tb);
        var bar = ButtonBar();
        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (s, e) => Close(_tb.Text);
        cancel.Click += (s, e) => Close((string)null);
        bar.Children.Add(ok); bar.Children.Add(cancel);
        sp.Children.Add(bar);
        Content = sp;
        Opened += (s, e) => { _tb.Focus(); _tb.SelectAll(); };
    }

    public static Task<string> Ask(Window owner, string title, string prompt, string def = "")
        => new InputDialog(title, prompt, def).ShowDialog<string>(owner);
}

/// <summary>Color picker: spectrum square + hue bar + hex field. No preset palette.</summary>
public class ColorDialog : DialogBase
{
    readonly ColorView _cv;
    readonly Action<string> _live;

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    ColorDialog(string title, string start, Action<string> live) : base(title, 360)
    {
        _live = live;
        var sp = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };
        _cv = new ColorView
        {
            Color = ThemeColors.C(start),
            IsAlphaEnabled = false, IsAlphaVisible = false,
            IsColorPaletteVisible = false, IsAccentColorsVisible = false,
            IsColorComponentsVisible = false, IsColorModelVisible = false,
            IsColorSpectrumVisible = true,
            IsColorPreviewVisible = true, IsHexInputVisible = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _cv.ColorChanged += (s, e) => _live?.Invoke(Hex(e.NewColor));
        sp.Children.Add(_cv);
        var bar = ButtonBar();
        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (s, e) => Close(Hex(_cv.Color));
        cancel.Click += (s, e) => Close((string)null);
        bar.Children.Add(ok); bar.Children.Add(cancel);
        sp.Children.Add(bar);
        Content = sp;
    }

    /// <summary>Returns the chosen color (#RRGGBB) or null if cancelled. "live" is called while the color changes.</summary>
    public static Task<string> Pick(Window owner, string title, string start, Action<string> live)
        => new ColorDialog(title, start, live).ShowDialog<string>(owner);
}
}

// ---- Panels.cs
namespace Notium
{
using System.IO.Enumeration;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

/// <summary>One search result.</summary>
public class Hit
{
    public TabDoc Tab;
    public string Path;
    public int Line;
    public string Text;
}

/// <summary>Search text in all open tabs or in a whole folder.</summary>
public class FindInFilesWindow : Window
{
    const int Max = 2000;
    readonly MainWindow _owner;
    readonly TextBox _q, _filter, _folder, _repl;
    readonly CheckBox _case, _regex;
    readonly ComboBox _scope;
    readonly ListBox _list;
    readonly TextBlock _info;
    CancellationTokenSource _cts;

    public FindInFilesWindow(MainWindow owner)
    {
        _owner = owner;
        Title = "Find and replace in files - Notium"; Width = 780; Height = 580; MinWidth = 520; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var c = AppSettings.Current.Colors;
        Background = ThemeColors.B(c.Background); Foreground = ThemeColors.B(c.Text);

        _q = new TextBox { Watermark = "Search for..." };
        _q.KeyDown += (s, e) => { if (e.Key == Key.Enter) { _ = RunAsync(); e.Handled = true; } };
        var go = new Button { Content = "Search", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        go.Click += (s, e) => _ = RunAsync();
        var row1 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        row1.Children.Add(_q);
        Grid.SetColumn(go, 1); go.Margin = new Thickness(8, 0, 0, 0); row1.Children.Add(go);

        _scope = new ComboBox { ItemsSource = new[] { "Open tabs", "Folder" }, SelectedIndex = string.IsNullOrEmpty(owner.SidebarFolder) ? 0 : 1, Width = 130 };
        _folder = new TextBox { Text = owner.SidebarFolder ?? "", Watermark = "Folder to search" };
        var browse = new Button { Content = "Browse..." };
        browse.Click += async (s, e) =>
        {
            var r = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder" });
            string p = r.FirstOrDefault()?.TryGetLocalPath();
            if (p != null) { _folder.Text = p; _scope.SelectedIndex = 1; }
        };
        _filter = new TextBox { Text = "*", Width = 160, Watermark = "Files, e.g. *.py;*.js" };
        var row2 = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        row2.Children.Add(_scope);
        Grid.SetColumn(_folder, 1); _folder.Margin = new Thickness(8, 0, 8, 0); row2.Children.Add(_folder);
        Grid.SetColumn(browse, 2); row2.Children.Add(browse);
        Grid.SetColumn(_filter, 3); _filter.Margin = new Thickness(8, 0, 0, 0); row2.Children.Add(_filter);

        _case = new CheckBox { Content = "Match case" };
        _regex = new CheckBox { Content = "Regular expression", Margin = new Thickness(16, 0, 0, 0) };
        var row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row3.Children.Add(_case); row3.Children.Add(_regex);

        _repl = new TextBox { Watermark = "Replace with... (then press the button to see the preview)" };
        var replBtn = new Button { Content = "Replace all...", MinWidth = 120, HorizontalContentAlignment = HorizontalAlignment.Center };
        replBtn.Click += (s, e) => _ = ReplaceAllAsync();
        var row4 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        row4.Children.Add(_repl);
        Grid.SetColumn(replBtn, 1); replBtn.Margin = new Thickness(8, 0, 0, 0); row4.Children.Add(replBtn);

        _list = new ListBox { Background = Brushes.Transparent, FontFamily = new FontFamily(AppSettings.FontFallback) };
        _list.DoubleTapped += async (s, e) =>
        {
            if (_list.SelectedItem is ListBoxItem li && li.Tag is Hit h) await _owner.OpenHit(h);
        };
        _info = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Opacity = 0.7, FontSize = 12, Text = "Double-click a result to open it." };

        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto"), Margin = new Thickness(18) };
        g.Children.Add(row1);
        Grid.SetRow(row2, 1); g.Children.Add(row2);
        Grid.SetRow(row3, 2); g.Children.Add(row3);
        Grid.SetRow(row4, 3); g.Children.Add(row4);
        Grid.SetRow(_list, 4); g.Children.Add(_list);
        Grid.SetRow(_info, 5); g.Children.Add(_info);
        Content = g;
        Opened += (s, e) => { _q.Focus(); };
        Closed += (s, e) => _cts?.Cancel();
    }

    // ---------------------------------------------------------------- replace in many files, with a preview first
    sealed class FileChange
    {
        public TabDoc Tab; public string Path, NewText, Encoding, Eol;
        public int Count;
        public List<(string oldLine, string newLine)> Samples = new();
    }

    static FileChange BuildChange(string text, Regex rx, string rep)
    {
        int n = rx.Matches(text).Count;
        if (n == 0) return null;
        var ch = new FileChange { Count = n, NewText = rx.Replace(text, rep) };
        foreach (var line in text.Split('\n'))
        {
            if (ch.Samples.Count >= 3) break;
            if (!rx.IsMatch(line)) continue;
            string o = line.Trim(), nw = rx.Replace(line, rep).Trim();
            ch.Samples.Add((o.Length > 160 ? o.Substring(0, 160) + "..." : o, nw.Length > 160 ? nw.Substring(0, 160) + "..." : nw));
        }
        return ch;
    }

    static List<string> ListFiles(string root, string filter, CancellationToken ct)
    {
        var pats = (string.IsNullOrWhiteSpace(filter) ? "*" : filter).Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var res = new List<string>();
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0 && res.Count < 20000)
        {
            ct.ThrowIfCancellationRequested();
            string dir = stack.Pop();
            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    string dn = System.IO.Path.GetFileName(d);
                    if (dn.StartsWith('.') || dn == "node_modules" || dn == "bin" || dn == "obj") continue;
                    stack.Push(d);
                }
                foreach (var f in Directory.EnumerateFiles(dir))
                    if (pats.Any(p => FileSystemName.MatchesSimpleExpression(p, System.IO.Path.GetFileName(f), true))) res.Add(f);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return res;
    }

    static List<FileChange> CollectFolder(string root, string filter, Regex rx, string rep, HashSet<string> skip, CancellationToken ct)
    {
        var res = new List<FileChange>();
        foreach (var f in ListFiles(root, filter, ct))
        {
            ct.ThrowIfCancellationRequested();
            if (res.Count >= 2000) break;
            if (skip.Contains(f)) continue;
            try
            {
                if (new FileInfo(f).Length > 5_000_000) continue;
                byte[] b = File.ReadAllBytes(f);
                bool bin = false;
                for (int i = 0, mx = Math.Min(b.Length, 4096); i < mx; i++) if (b[i] == 0) { bin = true; break; }
                if (bin) continue;
                var lf = TabDoc.Read(f);
                var ch = BuildChange(lf.Text, rx, rep);
                if (ch == null) continue;
                ch.Path = f; ch.Encoding = lf.Encoding; ch.Eol = lf.Eol;
                res.Add(ch);
            }
            catch { }
        }
        return res;
    }

    async Task ReplaceAllAsync()
    {
        string pat = _q.Text;
        if (string.IsNullOrEmpty(pat)) { _info.Text = "Type what to search for first."; return; }
        Regex rx;
        try { rx = new Regex(_regex.IsChecked == true ? pat : Regex.Escape(pat), _case.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase); }
        catch { _info.Text = "Invalid expression"; return; }
        string repl = _repl.Text ?? "";
        string rep = _regex.IsChecked == true ? repl : repl.Replace("$", "$$");      // without regular expressions, "$" is just a character

        _cts?.Cancel(); _cts = new CancellationTokenSource(); var ct = _cts.Token;
        _info.Text = "Preparing the preview...";
        var changes = new List<FileChange>();
        try
        {
            if (_scope.SelectedIndex == 0)
            {
                foreach (var t in _owner.Tabs.ToList())
                {
                    var ch = BuildChange(t.Doc.Text, rx, rep);
                    if (ch != null) { ch.Tab = t; ch.Path = t.Path; changes.Add(ch); }
                }
            }
            else
            {
                string folder = _folder.Text?.Trim();
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { _info.Text = "Choose an existing folder."; return; }
                string filter = _filter.Text;
                var openPaths = new HashSet<string>(_owner.Tabs.Where(t => t.Path != null).Select(t => t.Path), StringComparer.OrdinalIgnoreCase);
                changes.AddRange(await Task.Run(() => CollectFolder(folder, filter, rx, rep, openPaths, ct), ct));
                // files that are open in a tab are changed in the tab (so unsaved work is never lost)
                var pats = (string.IsNullOrWhiteSpace(filter) ? "*" : filter).Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in _owner.Tabs.ToList())
                {
                    if (t.Path == null || !t.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!pats.Any(p => FileSystemName.MatchesSimpleExpression(p, System.IO.Path.GetFileName(t.Path), true))) continue;
                    var ch = BuildChange(t.Doc.Text, rx, rep);
                    if (ch != null) { ch.Tab = t; ch.Path = t.Path; changes.Add(ch); }
                }
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { _info.Text = ex.Message; return; }

        if (changes.Count == 0) { _info.Text = "Nothing to replace."; return; }
        int total = changes.Sum(x => x.Count);
        string summary = $"{total} replacement{(total == 1 ? "" : "s")} in {changes.Count} file{(changes.Count == 1 ? "" : "s")}.";
        var preview = new List<(string text, int kind)>();
        foreach (var ch in changes.Take(300))
        {
            string name = ch.Path ?? ch.Tab?.Title ?? "";
            preview.Add(((ch.Tab != null ? "[open tab] " : "") + name + "   (" + ch.Count + ")", 0));
            foreach (var (o, n) in ch.Samples) { preview.Add(("-  " + o, 1)); preview.Add(("+  " + n, 2)); }
        }
        if (changes.Count > 300) preview.Add(($"... and {changes.Count - 300} more files", 0));
        string note = changes.Any(x => x.Tab != null) ? "Open tabs are changed but not saved: you can undo with Ctrl+Z. Files on disk are changed immediately." : "The files on disk are changed immediately. There is no undo.";
        if (!await ReplacePreviewDialog.Ask(this, summary, note, preview)) { _info.Text = "Replace cancelled."; return; }

        int done = 0, failed = 0;
        foreach (var ch in changes.Where(x => x.Tab != null)) { DocEdit.ReplaceMinimal(ch.Tab.Doc, ch.NewText); done++; }
        var disk = changes.Where(x => x.Tab == null).ToList();
        if (disk.Count > 0)
        {
            int ok = await Task.Run(() =>
            {
                int n2 = 0;
                foreach (var ch in disk) { try { DocEdit.WriteFile(ch.Path, ch.NewText, ch.Encoding, ch.Eol); n2++; } catch { } }
                return n2;
            });
            done += ok; failed = disk.Count - ok;
        }
        _list.Items.Clear();
        _info.Text = $"Replaced {total} occurrence{(total == 1 ? "" : "s")} in {done} file{(done == 1 ? "" : "s")}." + (failed > 0 ? $" {failed} file(s) could not be written." : "");
    }

    async Task RunAsync()
    {
        string pat = _q.Text;
        if (string.IsNullOrEmpty(pat)) return;
        Regex rx;
        try { rx = new Regex(_regex.IsChecked == true ? pat : Regex.Escape(pat), _case.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase); }
        catch { _info.Text = "Invalid expression"; return; }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _list.Items.Clear();
        _info.Text = "Searching...";
        var hits = new List<Hit>();
        int files = 0;
        try
        {
            if (_scope.SelectedIndex == 0)
            {
                foreach (var t in _owner.Tabs.ToList())
                {
                    files++;
                    SearchText(t.Doc.Text, t, t.Path, rx, hits);
                    if (hits.Count >= Max) break;
                }
            }
            else
            {
                string folder = _folder.Text?.Trim();
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { _info.Text = "Choose an existing folder."; return; }
                string filter = _filter.Text;
                files = await Task.Run(() => SearchFolder(folder, filter, rx, hits, ct), ct);
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { _info.Text = ex.Message; return; }
        if (ct.IsCancellationRequested) return;

        foreach (var h in hits)
        {
            string name = h.Path != null ? System.IO.Path.GetFileName(h.Path) : h.Tab?.Title ?? "";
            _list.Items.Add(new ListBoxItem { Content = $"{name}:{h.Line}    {h.Text}", Tag = h });
        }
        _info.Text = hits.Count == 0 ? "No results." : $"{hits.Count} result{(hits.Count == 1 ? "" : "s")} in {files} file{(files == 1 ? "" : "s")}" + (hits.Count >= Max ? " (limit reached)" : "") + ". Double-click a result to open it.";
    }

    static void SearchText(string text, TabDoc tab, string path, Regex rx, List<Hit> hits)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length && hits.Count < Max; i++)
        {
            if (!rx.IsMatch(lines[i])) continue;
            string t = lines[i].Trim();
            if (t.Length > 200) t = t.Substring(0, 200) + "...";
            hits.Add(new Hit { Tab = tab, Path = path, Line = i + 1, Text = t });
        }
    }

    static int SearchFolder(string root, string filter, Regex rx, List<Hit> hits, CancellationToken ct)
    {
        var pats = (string.IsNullOrWhiteSpace(filter) ? "*" : filter).Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<string>();
        stack.Push(root);
        int files = 0;
        while (stack.Count > 0 && hits.Count < Max)
        {
            ct.ThrowIfCancellationRequested();
            string dir = stack.Pop();
            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    string dn = System.IO.Path.GetFileName(d);
                    if (dn.StartsWith('.') || dn == "node_modules" || dn == "bin" || dn == "obj") continue;
                    stack.Push(d);
                }
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    if (hits.Count >= Max) break;
                    string n = System.IO.Path.GetFileName(f);
                    if (!pats.Any(p => FileSystemName.MatchesSimpleExpression(p, n, true))) continue;
                    if (new FileInfo(f).Length > 5_000_000) continue;
                    byte[] b = File.ReadAllBytes(f);
                    bool bin = false;
                    for (int i = 0, m = Math.Min(b.Length, 4096); i < m; i++) if (b[i] == 0) { bin = true; break; }
                    if (bin) continue;
                    files++;
                    SearchText(Enc.Decode(b, out _), null, f, rx, hits);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return files;
    }
}

/// <summary>Searchable list of every programming language.</summary>
public class LanguageDialog : DialogBase
{
    readonly ListBox _list;
    readonly TextBox _filter;
    readonly List<(string id, string name)> _all = new();
    readonly string _current;

    LanguageDialog(string current) : base("Language", 380)
    {
        _current = current;
        _all.Add((Syntax.Plain, Syntax.Plain));
        foreach (var id in Syntax.AllIds()) _all.Add((id, Syntax.Display(id)));

        var sp = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };
        sp.Children.Add(new TextBlock { Text = "Choose the language used for coloring:", Margin = new Thickness(0, 0, 0, 8) });
        _filter = new TextBox { Watermark = "Type to filter..." };
        sp.Children.Add(_filter);
        _list = new ListBox { Height = 300, Margin = new Thickness(0, 8, 0, 0), Background = Brushes.Transparent };
        sp.Children.Add(_list);
        Fill();
        _filter.PropertyChanged += (s, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        _list.DoubleTapped += (s, e) => Pick();

        var bar = ButtonBar();
        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (s, e) => Pick();
        cancel.Click += (s, e) => Close((string)null);
        bar.Children.Add(ok); bar.Children.Add(cancel);
        sp.Children.Add(bar);
        Content = sp;
        Opened += (s, e) => _filter.Focus();
    }

    void Fill()
    {
        string f = _filter.Text?.Trim() ?? "";
        _list.Items.Clear();
        ListBoxItem sel = null;
        foreach (var (id, name) in _all)
        {
            if (f.Length > 0 && name.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0 && id.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var li = new ListBoxItem { Content = name, Tag = id };
            _list.Items.Add(li);
            if (id == _current) sel = li;
        }
        if (sel != null) _list.SelectedItem = sel;
        else if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    void Pick()
    {
        if (_list.SelectedItem is ListBoxItem li) Close((string)li.Tag);
    }

    public static Task<string> Choose(Window owner, string current)
        => new LanguageDialog(current).ShowDialog<string>(owner);
}
}

// ---- SettingsWindow.cs
namespace Notium
{
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

public class SettingsWindow : Window
{
    static SettingsWindow _inst;
    ListBox _nav;
    ContentControl _host;
    Border _navBg;
    int _page;

    static AppSettings S => AppSettings.Current;
    static IBrush Br(string hex) => ThemeColors.B(hex);
    static string Chrome => S.Colors.Chrome;

    public static void ShowSingle(Window owner)
    {
        if (_inst != null) { _inst.Activate(); return; }
        _inst = new SettingsWindow();
        _inst.Closed += (s, e) => _inst = null;
        _inst.Show(owner);
    }

    SettingsWindow()
    {
        Title = "Settings - Notium"; Width = 950; Height = 640; MinWidth = 700; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Fx.SoftDrag(this);
        Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://Notium.App/Assets/notium.png")));

        var pages = new (string name, Func<Control> build)[]
        {
            ("Appearance", PgAppearance), ("Themes", PgThemes), ("Custom colors", PgColors), ("Editor", PgEditor),
            ("Line numbers", PgNumbers), ("Minimap", PgMinimap), ("Files and saving", PgFiles),
            ("Keyboard shortcuts", PgKeys), ("Windows and panels", PgWindows), ("Privacy", PgPrivacy), ("Advanced settings", PgAdvanced)
        };

        _nav = new ListBox { Margin = new Thickness(0), Padding = new Thickness(10, 4, 10, 8), Background = Brushes.Transparent, Classes = { "nav" } };
        foreach (var p in pages) _nav.Items.Add(new ListBoxItem { Content = p.name, Tag = p.build });
        _nav.SelectionChanged += (s, e) =>
        {
            if (_nav.SelectedItem is ListBoxItem li) { _page = _nav.SelectedIndex; ShowPage((Func<Control>)li.Tag); if (_host.Content is Control pg) Fx.FadeIn(pg, 130); }
        };

        _host = new ContentControl();
        var sv = new ScrollViewer { Content = _host, Padding = new Thickness(28, 20, 28, 20), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var navHead = new TextBlock { Text = "SETTINGS", FontSize = 11, FontWeight = FontWeight.SemiBold, Opacity = 0.5, Margin = new Thickness(22, 20, 0, 10) };
        var navGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(_nav, 1);
        navGrid.Children.Add(navHead); navGrid.Children.Add(_nav);
        _navBg = new Border { Child = navGrid, Width = 220 };

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*") };
        Grid.SetColumn(sv, 1);
        g.Children.Add(_navBg); g.Children.Add(sv);
        Content = g;

        ApplyColors();
        AppSettings.Changed += OnChanged;
        Closed += (s, e) => AppSettings.Changed -= OnChanged;
        _sig = Signature();
        _nav.SelectedIndex = 0;
    }

    void ApplyColors()
    {
        Background = Br(S.Colors.Background);
        Foreground = Br(S.Colors.Text);
        _navBg.Background = Br(Chrome);
    }

    // Refresh this window when the theme changes. Pages are rebuilt only when colors/themes changed,
    // so dragging a slider is never interrupted.
    string _sig;
    static string Signature() => JsonSerializer.Serialize(S.Colors) + "|" + S.CustomThemes.Count;

    void OnChanged()
    {
        ApplyColors();
        string sig = Signature();
        if (sig == _sig || _busy) return;
        _sig = sig;
        if (_nav.SelectedItem is ListBoxItem li) ShowPage((Func<Control>)li.Tag);
    }

    bool _busy;
    void ShowPage(Func<Control> build) => _host.Content = build();

    // ------------------------------------------------------------ building blocks
    static StackPanel Page(string title)
    {
        var p = new StackPanel { MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
        p.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeight.SemiBold });
        p.Children.Add(new Border { Width = 36, Height = 3, CornerRadius = new CornerRadius(2), Background = Br(S.Colors.Accent), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 20) });
        return p;
    }

    static Border Card(params Control[] rows)
    {
        var sp = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) sp.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2), Background = Br(S.Colors.Hover), Opacity = 0.7 });
            sp.Children.Add(rows[i]);
        }
        return new Border
        {
            CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(18, 10),
            Margin = new Thickness(0, 0, 0, 14), Child = sp, Background = Br(Chrome), Classes = { "card" }
        };
    }

    static Grid Row(string label, string desc, Control ctl)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6) };
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 14, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(desc))
            sp.Children.Add(new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = 0.6 });
        g.Children.Add(sp);
        ctl.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(ctl, 1);
        g.Children.Add(ctl);
        return g;
    }

    static ToggleSwitch Switch(bool on, Action<bool> set)
    {
        var sw = new ToggleSwitch { IsChecked = on, OnContent = "", OffContent = "" };
        sw.IsCheckedChanged += (s, e) => { set(sw.IsChecked == true); AppSettings.Notify(); };
        return sw;
    }

    static Grid Toggle(string label, string desc, bool on, Action<bool> set) => Row(label, desc, Switch(on, set));

    static Grid Slide(string label, string desc, double min, double max, double val, string suffix, Action<double> set)
    {
        var sl = new Slider { Minimum = min, Maximum = max, Value = val, Width = 170, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        var tx = new TextBlock { Text = val + suffix, Width = 56, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        sl.PropertyChanged += (s, e) =>
        {
            if (e.Property != RangeBase.ValueProperty) return;
            tx.Text = sl.Value + suffix; set(sl.Value); AppSettings.Notify();
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(sl); sp.Children.Add(tx);
        return Row(label, desc, sp);
    }

    static Grid Combo(string label, string desc, IEnumerable<string> items, string sel, Action<string> set, double width = 190)
    {
        var cb = new ComboBox { Width = width, ItemsSource = items.ToList(), SelectedItem = sel };
        cb.SelectionChanged += (s, e) => { if (cb.SelectedItem is string x) { set(x); AppSettings.Notify(); } };
        return Row(label, desc, cb);
    }

    static TextBlock Info(string t) => new TextBlock { Text = t, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6), FontSize = 13.5, LineHeight = 21, Opacity = 0.85 };

    static Button Btn(string text, Action a)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 6), Margin = new Thickness(0, 0, 8, 0) };
        b.Click += (s, e) => a();
        return b;
    }

    // ------------------------------------------------------------ pages
    Control PgAppearance()
    {
        var p = Page("Appearance");
        var names = ThemeColors.BuiltIn.Select(t => t.Name).Concat(S.CustomThemes.Select(t => t.Name)).Distinct();
        p.Children.Add(Card(
            Combo("Theme", "Instantly applies the colors of the chosen theme", names, S.Colors.Name, n =>
            {
                var t = ThemeColors.BuiltIn.Concat(S.CustomThemes).FirstOrDefault(x => x.Name == n);
                if (t != null) S.Colors = t.Clone();
            }),
            Slide("Interface size", "Tab bar and status bar", 80, 160, S.UiScale, "%", v => S.UiScale = (int)v),
            Toggle("Status bar", "Show the bar at the bottom", S.ShowStatusBar, v => S.ShowStatusBar = v)));
        return p;
    }

    Control PgThemes()
    {
        var p = Page("Themes");
        p.Children.Add(new TextBlock { Text = "Built-in themes", FontSize = 15, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        var w1 = new WrapPanel();
        foreach (var t in ThemeColors.BuiltIn) w1.Children.Add(ThemeCard(t, false));
        p.Children.Add(w1);
        p.Children.Add(new TextBlock { Text = "Custom themes", FontSize = 15, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 16, 0, 8) });
        var w2 = new WrapPanel();
        foreach (var t in S.CustomThemes.ToList()) w2.Children.Add(ThemeCard(t, true));
        if (S.CustomThemes.Count == 0) w2.Children.Add(new TextBlock { Text = "No custom themes saved.", Opacity = 0.7 });
        p.Children.Add(w2);

        var bar = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        bar.Children.Add(Btn("Save current colors as theme", async () =>
        {
            string n = await InputDialog.Ask(this, "Theme name", "Name of the new theme:", S.Colors.Name + " custom");
            if (string.IsNullOrWhiteSpace(n)) return;
            var t = S.Colors.Clone(); t.Name = n.Trim();
            S.CustomThemes.RemoveAll(x => x.Name == t.Name); S.CustomThemes.Add(t);
            S.Colors.Name = t.Name; AppSettings.Notify();
        }));
        bar.Children.Add(Btn("Import...", async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter = new[] { new FilePickerFileType("Notium theme") { Patterns = new[] { "*.notiumtheme" } } }
            });
            string path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return;
            try
            {
                var t = JsonSerializer.Deserialize<ThemeColors>(File.ReadAllText(path));
                if (t == null) return;
                S.CustomThemes.RemoveAll(x => x.Name == t.Name); S.CustomThemes.Add(t);
                AppSettings.Notify();
            }
            catch (Exception ex) { await MessageDialog.Error(this, "Invalid file:\n" + ex.Message); }
        }));
        bar.Children.Add(Btn("Export current theme...", async () =>
        {
            var f = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = S.Colors.Name + ".notiumtheme", DefaultExtension = "notiumtheme",
                FileTypeChoices = new[] { new FilePickerFileType("Notium theme") { Patterns = new[] { "*.notiumtheme" } } }
            });
            string path = f?.TryGetLocalPath();
            if (path != null) File.WriteAllText(path, JsonSerializer.Serialize(S.Colors, new JsonSerializerOptions { WriteIndented = true }));
        }));
        p.Children.Add(bar);
        return p;
    }

    Control ThemeCard(ThemeColors t, bool deletable)
    {
        var inner = new StackPanel { Margin = new Thickness(10, 8) };
        inner.Children.Add(new Border { Height = 5, Width = 40, CornerRadius = new CornerRadius(2), Background = Br(t.Accent), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 7) });
        inner.Children.Add(new TextBlock { Text = t.Name, Foreground = Br(t.Text), FontWeight = FontWeight.SemiBold, FontSize = 13 });
        inner.Children.Add(new TextBlock { Text = "Sample text", Foreground = Br(t.LineNumbers), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        inner.Children.Add(new Border { Height = 12, Background = Br(t.Selection), Margin = new Thickness(0, 6, 0, 0), CornerRadius = new CornerRadius(2) });
        var card = new Border
        {
            Width = 150, Height = 96, CornerRadius = new CornerRadius(8), Background = Br(t.Background), Child = inner,
            BorderThickness = new Thickness(2), Cursor = new Cursor(StandardCursorType.Hand), Margin = new Thickness(0, 0, 10, 10),
            BorderBrush = t.Name == S.Colors.Name ? Br(t.Accent) : Br(t.Hover)
        };
        card.PointerReleased += (s, e) => { S.Colors = t.Clone(); AppSettings.Notify(); };
        if (deletable)
        {
            var del = new MenuItem { Header = "Delete theme" };
            del.Click += (s, e) => { S.CustomThemes.Remove(t); AppSettings.Notify(); };
            card.ContextMenu = new ContextMenu { Items = { del } };
            ToolTip.SetTip(card, "Right-click to delete");
        }
        return card;
    }

    static readonly (string label, string prop)[] ColorDefs =
    {
        ("Editor background", "Background"), ("Text", "Text"), ("Selection", "Selection"), ("Caret", "Caret"),
        ("Current line", "CurrentLine"), ("Line numbers", "LineNumbers"), ("Minimap background", "MinimapBg"),
        ("Title bar and status bar", "Chrome"), ("Active tab", "ActiveTab"), ("Mouse hover", "Hover"), ("Accent color", "Accent")
    };

    Control PgColors()
    {
        var p = Page("Custom colors");
        var rows = new List<Control>();
        foreach (var (label, prop) in ColorDefs)
        {
            var pi = typeof(ThemeColors).GetProperty(prop);
            string cur = (string)pi.GetValue(S.Colors);
            var sw = new Border { Width = 34, Height = 22, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), BorderBrush = Br(S.Colors.Text), Background = Br(cur), Cursor = new Cursor(StandardCursorType.Hand) };
            var hex = new TextBlock { Text = cur.ToUpperInvariant(), Width = 76, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0), FontFamily = new FontFamily("Consolas, Menlo, monospace") };
            sw.PointerReleased += async (s, e) =>
            {
                string old = (string)pi.GetValue(S.Colors);
                _busy = true;                                   // keep the page while the picker is open
                string res = await ColorDialog.Pick(this, label, old, h => { pi.SetValue(S.Colors, h); AppSettings.Notify(); });
                _busy = false;
                pi.SetValue(S.Colors, res ?? old); AppSettings.Notify();
            };
            var reset = Btn("↺", () => { pi.SetValue(S.Colors, pi.GetValue(BaseTheme())); AppSettings.Notify(); });
            reset.Margin = new Thickness(0); ToolTip.SetTip(reset, "Reset this color");
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(sw); sp.Children.Add(hex); sp.Children.Add(reset);
            rows.Add(Row(label, null, sp));
        }
        p.Children.Add(Card(rows.ToArray()));
        p.Children.Add(Btn("Reset all colors", () => { S.Colors = BaseTheme().Clone(); AppSettings.Notify(); }));
        return p;
    }

    static ThemeColors BaseTheme() =>
        ThemeColors.BuiltIn.Concat(S.CustomThemes).FirstOrDefault(t => t.Name == S.Colors.Name) ?? ThemeColors.BuiltIn[0];

    Control PgEditor()
    {
        var p = Page("Editor");
        var fonts = FontManager.Current.SystemFonts.Select(f => f.Name).Distinct().OrderBy(x => x).ToList();
        string cur = S.FontFamily.Split(',')[0].Trim();
        if (!fonts.Contains(cur)) fonts.Insert(0, cur);
        p.Children.Add(Card(
            Combo("Font", null, fonts, cur, v => S.FontFamily = v, 240),
            Slide("Size", null, 8, 40, S.FontSize, " pt", v => S.FontSize = v),
            Toggle("Bold", null, S.Bold, v => S.Bold = v),
            Slide("Tab size", null, 1, 12, S.TabSize, "", v => S.TabSize = (int)v)));
        p.Children.Add(Card(
            Toggle("Word wrap", "Text wraps at the window edge", S.WordWrap, v => S.WordWrap = v),
            Toggle("Visible spaces and tabs", null, S.ShowWhitespace, v => S.ShowWhitespace = v),
            Toggle("Highlight the current line", null, S.HighlightCurrentLine, v => S.HighlightCurrentLine = v)));
        return p;
    }

    Control PgNumbers()
    {
        var p = Page("Line numbers");
        p.Children.Add(Card(Toggle("Show line numbers", "Off by default", S.ShowLineNumbers, v => S.ShowLineNumbers = v)));
        return p;
    }

    Control PgMinimap()
    {
        var p = Page("Minimap");
        p.Children.Add(Card(
            Toggle("Show the minimap", "A small preview of the document on the right side", S.ShowMinimap, v => S.ShowMinimap = v),
            Slide("Width", null, 60, 260, S.MinimapWidth, " px", v => S.MinimapWidth = (int)v)));
        p.Children.Add(Info("The minimap background color can be changed in Custom colors."));
        return p;
    }

    Control PgFiles()
    {
        var p = Page("Files and saving");
        p.Children.Add(Card(
            Combo("Default encoding", "For new documents", Enc.Names, S.DefaultEncoding, v => S.DefaultEncoding = v),
            Combo("Default line ending", "For new documents", new[] { "CRLF", "LF", "CR" }, S.DefaultLineEnding, v => S.DefaultLineEnding = v)));
        p.Children.Add(Card(
            Toggle("Restore tabs on start", "Reopen the tabs (and unsaved text) that were open when you quit. On by default", S.RestoreSession, v => { S.RestoreSession = v; if (!v) Session.Clear(); }),
            Toggle("Reload changed files automatically", "Files modified by another program reload by themselves unless you edited them here", S.AutoReload, v => S.AutoReload = v),
            Toggle("Keep a backup when saving", "Copies the old file to name.ext.bak before overwriting it", S.BackupOnSave, v => S.BackupOnSave = v)));
        p.Children.Add(Card(
            Toggle("Clean up text when saving", "Applies the options below every time a file is saved. On by default", S.CleanOnSave, v => S.CleanOnSave = v),
            Toggle("Remove spaces at the end of lines", "Not applied to Markdown, where they mean a line break", S.CleanTrailing, v => S.CleanTrailing = v),
            Toggle("Keep at most one empty line in a row", "Several empty lines in a row become one", S.CleanBlankLines, v => S.CleanBlankLines = v),
            Toggle("Remove double spaces inside lines", "Indentation is kept. Off by default: it can change aligned text and code", S.CleanDoubleSpaces, v => S.CleanDoubleSpaces = v),
            Toggle("Convert tabs to spaces", "Off by default. Not good for Makefiles", S.CleanTabsToSpaces, v => S.CleanTabsToSpaces = v)));
        p.Children.Add(Info("The same actions are always available by hand in Edit > Transform. Files are saved through a temporary file, so the original is never left half-written. New documents are .txt: to save another type (.py, .js, .html, ...) just type the extension in the file name."));
        return p;
    }

    Control PgKeys()
    {
        var p = Page("Keyboard shortcuts");
        p.Children.Add(Info("Click a shortcut, then press the new key combination. It needs Ctrl or Alt (function keys can stand alone). On macOS, Cmd counts as Ctrl. Changes apply at once."));
        var resetAll = Btn("Reset all to defaults", async () =>
        {
            if (await MessageDialog.Show(this, "Reset every shortcut to its default?", "Reset all", "Cancel") != 0) return;
            Keymap.ResetAll(); AppSettings.Notify(); ShowPage(PgKeys);
        });
        resetAll.Margin = new Thickness(0, 0, 0, 12);
        p.Children.Add(resetAll);

        var rows = new List<Control>();
        foreach (var c in Keymap.All)
        {
            var cmd = c;
            var change = new Button
            {
                Content = Keymap.Display(Keymap.Get(cmd.Id)), MinWidth = 160, HorizontalContentAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Consolas, Menlo, monospace"), FontSize = 13
            };
            ToolTip.SetTip(change, "Click to change");
            change.Click += async (s2, e2) =>
            {
                string g = await ShortcutDialog.Capture(this, cmd.Name);
                if (g != null) await AssignShortcut(cmd, g);
            };
            var reset = Btn("Reset", () => { Keymap.Reset(cmd.Id); AppSettings.Notify(); ShowPage(PgKeys); });
            reset.IsVisible = Keymap.IsCustom(cmd.Id);
            var box = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            box.Children.Add(change); box.Children.Add(reset);
            rows.Add(Row(cmd.Name, null, box));
        }
        p.Children.Add(Card(rows.ToArray()));
        p.Children.Add(Info("Other keys keep working as usual (Ctrl+C, Ctrl+V, Ctrl+Z, Ctrl+A...). Zooming with Ctrl + mouse wheel cannot be changed."));
        return p;
    }

    async Task AssignShortcut(CommandInfo cmd, string g)
    {
        if (g != "")
        {
            string other = Keymap.FindId(g, cmd.Id);
            if (other != null)
            {
                var o = Keymap.Info(other);
                if (await MessageDialog.Show(this, $"\"{g}\" is already used by \"{o.Name}\".\nUse it for \"{cmd.Name}\" instead?", "Use it here", "Cancel") != 0) return;
                Keymap.Set(other, "");
            }
        }
        Keymap.Set(cmd.Id, g);
        AppSettings.Notify();
        ShowPage(PgKeys);
    }

    Control PgWindows()
    {
        var p = Page("Windows and panels");
        p.Children.Add(Card(
            Toggle("Status bar", "Position, language, line ending, encoding and zoom; each one is clickable", S.ShowStatusBar, v => S.ShowStatusBar = v),
            Toggle("Folder panel", "Files of a folder and recent documents (Ctrl+B)", S.ShowSidebar, v => S.ShowSidebar = v),
            Toggle("Remember the window size", "Notium opens with the size you left it, saved in the data folder", S.RememberWindowSize, v => S.RememberWindowSize = v)));
        p.Children.Add(Info("You can open several windows with Ctrl+Shift+N. The X closes only the window where you press it; closing the last one closes the program. Drag a tab out of the tab bar to detach it into a new window, or onto the tab bar of another window to move it."));
        return p;
    }

    Control PgPrivacy()
    {
        var p = Page("Privacy");
        p.Children.Add(Card(Info("Notium works completely offline: no Internet connection, no account, no artificial intelligence, no ads and no telemetry.\n\nSettings and themes are saved only in the \"data\" folder next to the program. Nothing is written to AppData, the registry or your home folder.")));
        return p;
    }

    Control PgAdvanced()
    {
        var p = Page("Advanced settings");
        p.Children.Add(Card(
            Row("Data folder", AppSettings.DataDir, Btn("Open folder", OpenDataFolder)),
            Row("Reset settings", "Restores everything to the defaults (custom themes are deleted)", Btn("Reset", async () =>
            {
                int r = await MessageDialog.Show(this, "Reset all settings?", "Reset", "Cancel");
                if (r == 0) AppSettings.Reset();
            }))));
        return p;
    }

    static void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            string cmd = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            Process.Start(new ProcessStartInfo(cmd, "\"" + AppSettings.DataDir + "\"") { UseShellExecute = false });
        }
        catch { }
    }
}
}

// ---- Minimap.cs
namespace Notium
{
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;

public class Minimap : Control
{
    TextEditor _ed;
    bool _drag;
    (double, double, double, int) _last;
    const double LineH = 2.5;

    public void Attach(TextEditor ed)
    {
        _ed = ed;
        // light polling keeps the minimap in sync with scrolling and typing without relying on editor events
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        t.Tick += (s, e) =>
        {
            if (!IsVisible || _ed?.Document == null) return;
            GetScroll(out double extent, out double view, out double off);
            var key = (extent, view, off, _ed.Document.LineCount);
            if (key != _last) { _last = key; InvalidateVisual(); }
        };
        t.Start();
    }

    public void Refresh() => InvalidateVisual();

    IScrollable Scroller =>
        _ed?.TextArea as IScrollable ?? _ed?.TextArea?.TextView as IScrollable;

    void GetScroll(out double extent, out double view, out double off)
    {
        var sc = Scroller;
        extent = sc?.Extent.Height ?? 0; view = sc?.Viewport.Height ?? 0; off = sc?.Offset.Y ?? 0;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        InvalidateVisual();
        return base.ArrangeOverride(finalSize);
    }

    double Total => (_ed?.Document?.LineCount ?? 1) * LineH;

    double MapTop(double extent, double view, double offset)
    {
        double avail = Bounds.Height;
        if (Total <= avail) return 0;
        double range = Math.Max(1, extent - view);
        return Math.Clamp(offset / range, 0, 1) * (Total - avail);
    }

    public override void Render(DrawingContext dc)
    {
        var c = AppSettings.Current.Colors;
        double w = Bounds.Width, h = Bounds.Height;
        dc.DrawRectangle(ThemeColors.B(c.MinimapBg), null, new Rect(0, 0, w, h));
        if (_ed?.Document == null) return;

        var tc = ThemeColors.C(c.Text);
        var brush = new SolidColorBrush(Color.FromArgb(110, tc.R, tc.G, tc.B));
        GetScroll(out double extent, out double view, out double off);
        double top = MapTop(extent, view, off);
        int first = Math.Max(1, (int)(top / LineH)), last = Math.Min(_ed.Document.LineCount, (int)((top + h) / LineH) + 2);
        double maxW = w - 6;

        for (int i = first; i <= last; i++)
        {
            var line = _ed.Document.GetLineByNumber(i);
            if (line.Length == 0) continue;
            string s = _ed.Document.GetText(line.Offset, Math.Min(line.Length, 160));
            double y = (i - 1) * LineH - top;
            int x = 0;
            while (x < s.Length)
            {
                if (char.IsWhiteSpace(s[x])) { x += s[x] == '\t' ? 4 : 1; continue; }
                int st = x;
                while (x < s.Length && !char.IsWhiteSpace(s[x])) x++;
                double rx = 3 + st, rw = x - st;
                if (rx > maxW) break;
                dc.DrawRectangle(brush, null, new Rect(rx, y, Math.Min(rw, maxW - rx), LineH - 0.8));
            }
        }

        if (extent > 0)
        {
            double vy = off / extent * Total - top;
            double vh = Math.Max(10, view / extent * Total);
            var ac = ThemeColors.C(c.Accent);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(45, ac.R, ac.G, ac.B)),
                new Pen(new SolidColorBrush(Color.FromArgb(120, ac.R, ac.G, ac.B)), 1),
                new Rect(0, vy, w, Math.Min(vh, h)));
        }
    }

    void ScrollTo(double y)
    {
        var sc = Scroller;
        if (sc == null) return;
        GetScroll(out double extent, out double view, out double off);
        double top = MapTop(extent, view, off);
        double frac = Math.Clamp((y + top) / Math.Max(1, Total), 0, 1);
        double target = Math.Clamp(frac * extent - view / 2, 0, Math.Max(0, extent - view));
        sc.Offset = new Vector(sc.Offset.X, target);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _drag = true; e.Pointer.Capture(this); ScrollTo(e.GetPosition(this).Y);
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_drag) ScrollTo(e.GetPosition(this).Y);
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _drag = false; e.Pointer.Capture(null);
        base.OnPointerReleased(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var sc = Scroller;
        if (sc != null) sc.Offset = new Vector(sc.Offset.X, Math.Max(0, sc.Offset.Y - e.Delta.Y * 60));
        base.OnPointerWheelChanged(e);
    }
}
}

// ---- Fx
namespace Notium
{
    using Avalonia;
    using Avalonia.Animation;
    using Avalonia.Controls;
    using Avalonia.Threading;

    /// <summary>Tiny fade-in used by dialogs, settings pages and the find panel. Never used on hover, to avoid flicker.</summary>
    public static class Fx
    {
        /// <summary>While a window is being dragged it becomes slightly transparent, and returns to normal a moment after it stops.</summary>
        public static void SoftDrag(Window w)
        {
            w.Transitions = new Transitions { new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(120) } };
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            timer.Tick += (s, e) => { timer.Stop(); w.Opacity = 1; };
            w.PositionChanged += (s, e) =>
            {
                if (!w.IsVisible) return;
                w.Opacity = 0.9;
                timer.Stop(); timer.Start();
            };
        }

        public static void FadeIn(Control c, double ms = 130)
        {
            c.Transitions = new Transitions { new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(ms) } };
            c.Opacity = 0;
            Dispatcher.UIThread.Post(() => c.Opacity = 1, DispatcherPriority.Loaded);
        }
    }
}

// ---- Printer
namespace Notium
{
    using System.Net;
    using System.Text;

    /// <summary>Builds a printable page (black on white, monospaced, wrapped lines). The browser shows the print preview.</summary>
    public static class Printer
    {
        public static string BuildHtml(string title, string text, bool lineNumbers)
        {
            string safeTitle = WebUtility.HtmlEncode(title);
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>").Append(safeTitle).Append("</title><style>");
            sb.Append("@page{margin:18mm 16mm}body{margin:0;color:#000;background:#fff;font:10pt/1.45 Consolas,'Cascadia Code',Menlo,'DejaVu Sans Mono',monospace}");
            sb.Append("h1{font:600 11pt system-ui,Segoe UI,Arial,sans-serif;margin:0 0 10pt;padding-bottom:6pt;border-bottom:1px solid #999}");
            sb.Append("table{border-collapse:collapse;width:100%}td{vertical-align:top;padding:0}");
            sb.Append("td.n{width:1%;white-space:nowrap;text-align:right;padding-right:10pt;color:#888;user-select:none}");
            sb.Append("td.t,pre{white-space:pre-wrap;word-break:break-word;overflow-wrap:anywhere;margin:0;font:inherit}");
            sb.Append(".bar{position:sticky;top:0;background:#f3f3f3;padding:8px 14px;font:13px system-ui,Segoe UI,Arial,sans-serif;border-bottom:1px solid #ccc}");
            sb.Append(".bar button{font:inherit;padding:4px 14px;margin-right:10px}.page{padding:14px 18px}");
            sb.Append("@media print{.bar{display:none}.page{padding:0}}</style></head><body>");
            sb.Append("<div class=\"bar\"><button onclick=\"window.print()\">Print</button>Use the print dialog to choose the printer and see the preview.</div>");
            sb.Append("<div class=\"page\"><h1>").Append(safeTitle).Append("</h1>");
            if (lineNumbers)
            {
                sb.Append("<table>");
                int n = 1;
                foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    sb.Append("<tr><td class=\"n\">").Append(n++).Append("</td><td class=\"t\">").Append(WebUtility.HtmlEncode(line)).Append("&#8203;</td></tr>");
                sb.Append("</table>");
            }
            else sb.Append("<pre>").Append(WebUtility.HtmlEncode(text)).Append("</pre>");
            sb.Append("</div><script>window.addEventListener('load',function(){setTimeout(function(){window.print()},350)})</script></body></html>");
            return sb.ToString();
        }
    }
}

// ---- Text tools
namespace Notium
{
    using System.Text;
    using System.Text.RegularExpressions;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Media;
    using AvaloniaEdit;
    using AvaloniaEdit.Document;
    using AvaloniaEdit.Rendering;

    /// <summary>Word counting and reading time.</summary>
    public static class TextStats
    {
        public static int Words(string s)
        {
            int n = 0; bool inWord = false;
            foreach (char ch in s)
            {
                bool w = !char.IsWhiteSpace(ch);
                if (w && !inWord) n++;
                inWord = w;
            }
            return n;
        }

        public static string Describe(string text, int lines, int chars)
        {
            int words = text.Length == 0 && chars > 0 ? -1 : Words(text);
            int noSpaces = text.Length == 0 ? 0 : text.Count(ch => !char.IsWhiteSpace(ch));
            string w = words < 0 ? "too large to count" : words.ToString();
            string read = words < 0 ? "-" : words == 0 ? "0 min" : words < 230 ? "less than 1 min" : "about " + (int)Math.Ceiling(words / 230.0) + " min";
            return $"Lines: {lines}\nWords: {w}\nCharacters: {chars}\nCharacters without spaces: {(words < 0 ? "-" : noSpaces.ToString())}\nReading time: {read}";
        }
    }

    /// <summary>Whitespace clean-up used by Edit > Transform and by "Clean up when saving".</summary>
    public static class TextClean
    {
        public static string TrimTrailing(string s) => Regex.Replace(s, @"[ \t]+(?=\r?\n|\z)", "");

        public static string DoubleSpaces(string s) => Regex.Replace(s, @"(?<=\S) {2,}(?=\S)", " ");

        public static string ExtraBlankLines(string s)
            => Regex.Replace(s, @"(?:\r?\n){3,}", m => m.Value.StartsWith("\r\n") ? "\r\n\r\n" : "\n\n");

        public static string TabsToSpaces(string s, int size)
        {
            size = Math.Clamp(size, 1, 16);
            var sb = new StringBuilder(s.Length);
            int col = 0;
            foreach (char ch in s)
            {
                if (ch == '\t') { int n = size - col % size; sb.Append(' ', n); col += n; }
                else { sb.Append(ch); col = ch == '\n' || ch == '\r' ? 0 : col + 1; }
            }
            return sb.ToString();
        }

        public static string SpacesToTabs(string s, int size)
        {
            size = Math.Clamp(size, 1, 16);
            return Regex.Replace(s, "(?m)^(?: {" + size + "})+", m => new string('\t', m.Length / size));
        }

        public static string SmartCase(string s)
            => Regex.Replace(s, @"(\A\s*|[.!?]\s+)(\p{Ll})", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());
    }

    /// <summary>Changes to a document and to files.</summary>
    public static class DocEdit
    {
        /// <summary>Replaces only the part of the text that differs, so the caret and the scroll position stay where they are.</summary>
        public static void ReplaceMinimal(TextDocument d, string newText)
        {
            string old = d.Text;
            if (old == newText) return;
            int p = 0, max = Math.Min(old.Length, newText.Length);
            while (p < max && old[p] == newText[p]) p++;
            int sfx = 0;
            while (sfx < max - p && old[old.Length - 1 - sfx] == newText[newText.Length - 1 - sfx]) sfx++;
            d.Replace(p, old.Length - p - sfx, newText.Substring(p, newText.Length - p - sfx));
        }

        /// <summary>Writes text (with "\n" line ends) the same way TabDoc.Save does: encoding, line ending, safe temporary file, optional backup.</summary>
        public static void WriteFile(string path, string text, string encoding, string eol)
        {
            string body = Enc.Normalize(text).Replace("\n", Enc.EolString(eol));
            var enc = Enc.Get(encoding);
            byte[] pre = enc.GetPreamble(), data = enc.GetBytes(body);
            string tmp = path + ".notium.tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write)) { fs.Write(pre, 0, pre.Length); fs.Write(data, 0, data.Length); }
            if (AppSettings.Current.BackupOnSave && File.Exists(path)) { try { File.Copy(path, path + ".bak", true); } catch { } }
            File.Move(tmp, path, true);
        }
    }

    /// <summary>Colors the lines that have a bookmark.</summary>
    public class BookmarkRenderer : IBackgroundRenderer
    {
        readonly Func<TabDoc> _tab;
        public BookmarkRenderer(Func<TabDoc> tab) { _tab = tab; }
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView tv, DrawingContext dc)
        {
            var t = _tab();
            if (t == null || t.Bookmarks.Count == 0 || !tv.VisualLinesValid) return;
            var lines = new HashSet<int>();
            foreach (var a in t.Bookmarks) if (!a.IsDeleted) lines.Add(a.Line);
            if (lines.Count == 0) return;
            var col = ThemeColors.C(AppSettings.Current.Colors.Accent);
            var fill = new SolidColorBrush(col, 0.20);
            var bar = new SolidColorBrush(col);
            foreach (var vl in tv.VisualLines)
            {
                if (!lines.Contains(vl.FirstDocumentLine.LineNumber)) continue;
                double y = vl.VisualTop - tv.ScrollOffset.Y;
                dc.DrawRectangle(fill, null, new Rect(0, y, tv.Bounds.Width, vl.Height));
                dc.DrawRectangle(bar, null, new Rect(0, y, 3, vl.Height));
            }
        }
    }

    /// <summary>Shows what "Replace all" is going to change and asks for confirmation.</summary>
    public class ReplacePreviewDialog : DialogBase
    {
        ReplacePreviewDialog(string summary, string note, List<(string text, int kind)> lines) : base("Replace in files - preview", 700)
        {
            var sp = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };
            sp.Children.Add(new TextBlock { Text = summary, FontSize = 16, FontWeight = FontWeight.SemiBold });
            sp.Children.Add(new TextBlock { Text = note, Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10) });
            var list = new StackPanel { Spacing = 1 };
            foreach (var (text, kind) in lines)
            {
                var tb = new TextBlock
                {
                    Text = text, FontFamily = new FontFamily(AppSettings.FontFallback), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(kind == 0 ? 0 : 14, kind == 0 ? 8 : 0, 0, 0), FontWeight = kind == 0 ? FontWeight.SemiBold : FontWeight.Normal
                };
                if (kind == 1) tb.Foreground = new SolidColorBrush(Color.Parse("#E06C75"));
                if (kind == 2) tb.Foreground = new SolidColorBrush(Color.Parse("#98C379"));
                list.Children.Add(tb);
            }
            sp.Children.Add(new ScrollViewer { Content = list, Height = 340, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
            var bar = ButtonBar();
            var ok = new Button { Content = "Replace", MinWidth = 100, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (s, e) => Close(true);
            cancel.Click += (s, e) => Close(false);
            bar.Children.Add(ok); bar.Children.Add(cancel);
            sp.Children.Add(bar);
            Content = sp;
        }

        public static async Task<bool> Ask(Window owner, string summary, string note, List<(string text, int kind)> lines)
            => await new ReplacePreviewDialog(summary, note, lines).ShowDialog<bool>(owner);
    }
}

// ---- TabGhost
namespace Notium
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;

    /// <summary>Small floating label with the name of a tab, shown under the mouse while the tab is dragged out of the tab bar.</summary>
    public class TabGhost : Window
    {
        TabGhost(string title)
        {
            var c = AppSettings.Current.Colors;
            SystemDecorations = SystemDecorations.None;
            ShowInTaskbar = false; ShowActivated = false; Topmost = true; CanResize = false; Focusable = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Opacity = 0.93;
            Content = new Border
            {
                Background = ThemeColors.B(c.Chrome), BorderBrush = ThemeColors.B(c.Accent), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8), IsHitTestVisible = false,
                Child = new TextBlock { Text = title, Foreground = ThemeColors.B(c.Text), FontSize = 13, MaxWidth = 240, TextTrimming = TextTrimming.CharacterEllipsis }
            };
        }

        public static TabGhost Open(string title)
        {
            var g = new TabGhost(title) { WindowStartupLocation = WindowStartupLocation.Manual };
            g.Show();
            return g;
        }
    }
}
