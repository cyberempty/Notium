// ---- SingleInstance.cs
// One running Notium per program folder. When the program is started again (for example by double-clicking a file),
// the new process hands the file names to the running one and quits, like Windows Notepad does.
namespace Notium
{
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

internal static class SingleInstance
{
    const string Header = "NOTIUM-OPEN-1";
    static Mutex _mutex;                       // held for the whole life of the first process
    static string _pipeName;
    static Action<string[]> _handler;
    static readonly List<string[]> _queue = new();   // requests that arrive before the window system is ready
    static readonly object _lock = new();

    /// <summary>The same folder always gives the same name; different portable copies do not disturb each other.</summary>
    static string Key()
    {
        string home = Environment.GetEnvironmentVariable("NOTIUM_HOME");
        if (string.IsNullOrWhiteSpace(home)) home = AppContext.BaseDirectory;
        home = home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows()) home = home.ToLowerInvariant();
        byte[] h = SHA256.HashData(Encoding.UTF8.GetBytes(home + "|" + Environment.UserName));
        return "Notium-" + Convert.ToHexString(h, 0, 8);
    }

    /// <summary>
    /// Called first thing in Main. Returns true when another Notium is already running and has received the files:
    /// the caller must then exit at once. Returns false when this process is the first one (or when anything goes wrong,
    /// so that a problem here never prevents Notium from starting).
    /// </summary>
    public static bool ForwardToRunning(string[] args)
    {
        try
        {
            _pipeName = Key();
            _mutex = new Mutex(true, _pipeName + "-lock", out bool first);
            if (first) { StartServer(); return false; }
            _mutex.Dispose(); _mutex = null;

            // the process started by the user may bring the other one to the front: allow it
            if (OperatingSystem.IsWindows()) { try { AllowSetForegroundWindow(-1); } catch { } }

            var paths = new List<string>();
            foreach (var a in args ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(a)) continue;
                try { paths.Add(Path.GetFullPath(a)); } catch { }   // relative names are relative to THIS process
            }
            // the first process may still be starting: try for a few seconds
            for (int i = 0; i < 8; i++)
            {
                if (TrySend(paths)) return true;
                Thread.Sleep(400);
            }
            return false;
        }
        catch { return false; }
    }

    static bool TrySend(List<string> paths)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            c.Connect(1500);
            using var w = new StreamWriter(c, new UTF8Encoding(false));
            w.WriteLine(Header);
            foreach (var p in paths) w.WriteLine(p);
            w.Flush();
            return true;
        }
        catch { return false; }
    }

    static void StartServer()
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var s = new NamedPipeServerStream(_pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte);
                    s.WaitForConnection();
                    using var r = new StreamReader(s, new UTF8Encoding(false));
                    if (r.ReadLine() != Header) continue;
                    var list = new List<string>();
                    string line;
                    while ((line = r.ReadLine()) != null) if (line.Length > 0) list.Add(line);
                    Deliver(list.ToArray());
                }
                catch { Thread.Sleep(200); }
            }
        }) { IsBackground = true, Name = "Notium single instance" };
        t.Start();
    }

    static void Deliver(string[] paths)
    {
        Action<string[]> h;
        lock (_lock)
        {
            h = _handler;
            if (h == null) { _queue.Add(paths); return; }
        }
        h(paths);
    }

    /// <summary>Called once the application is ready: from now on requests go to the handler (queued ones first).</summary>
    public static void Attach(Action<string[]> handler)
    {
        List<string[]> pending;
        lock (_lock)
        {
            _handler = handler;
            pending = new List<string[]>(_queue);
            _queue.Clear();
        }
        foreach (var p in pending) handler(p);
    }

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int dwProcessId);
}
}
