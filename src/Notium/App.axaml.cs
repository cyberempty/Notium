// ---- App.axaml.cs
namespace Notium
{
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

public partial class App : Application
{
    public static readonly List<MainWindow> Windows = new();
    public static IClassicDesktopStyleApplicationLifetime Lifetime;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Lifetime = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            AppSettings.Init();
            AppSettings.Changed += ApplyTheme;
            ApplyTheme();

            var w = new MainWindow();
            desktop.MainWindow = w;
            w.Show();
            w.StartUp(desktop.Args, !AppSettings.DataWritable);
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme()
    {
        var app = Current;
        if (app == null) return;
        var c = AppSettings.Current.Colors;
        var r = app.Resources;
        r["ChromeBrush"] = ThemeColors.B(c.Chrome);
        r["BgBrush"] = ThemeColors.B(c.Background);
        r["TextBrush"] = ThemeColors.B(c.Text);
        r["HoverBrush"] = ThemeColors.B(c.Hover);
        r["AccentBrush"] = ThemeColors.B(c.Accent);
        r["ActiveTabBrush"] = ThemeColors.B(c.ActiveTab);
        var t = ThemeColors.C(c.Text);
        r["MutedBrush"] = new SolidColorBrush(Color.FromArgb(150, t.R, t.G, t.B));
        app.RequestedThemeVariant = ThemeColors.IsDark(c.Background) ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    /// <summary>Quits the whole program when the last window is gone.</summary>
    public static void CloseIfLast()
    {
        if (Windows.Count == 0) Lifetime?.Shutdown();
    }
}
}

// ---- Program.cs
namespace Notium
{
using Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            try
            {
                string home = Environment.GetEnvironmentVariable("NOTIUM_HOME") ?? AppContext.BaseDirectory;
                File.WriteAllText(Path.Combine(home, "crash.log"), DateTime.Now + Environment.NewLine + ex);
            }
            catch { }
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
}
