using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace ChatMixOverlay;
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTest();
        if (args.Contains("--render")) return RenderPreview();
        using var mutex = new Mutex(true, @"Local\ChatMixOverlay", out var first);
        if (!first) { if (!args.Contains("--background")) SingleInstance.Send(args.Contains("--exit") ? "exit" : args.Contains("--memory") ? "memory" : "settings"); return 0; }
        if (args.Contains("--exit")) return 0;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var settings = Settings.Load(); Overlay? overlay = null;
        using var observer = new SonarObserver();
        using var trayIcon = Appearance.LoadIcon();
        using var tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "ChatMix Overlay — waiting for Sonar", Visible = true };
        string status = "Waiting for Sonar"; double? previous = null; SettingsWindow? window = null;
        void Present(double balance) { (overlay ??= new Overlay(settings)).Present(balance); }
        async void Preview() { foreach (var v in new[] { 0.0, -.85, 0, .85, 0 }) { if (app.Dispatcher.HasShutdownStarted) return; Present(v); await Task.Delay(420); } }
        void OpenSettings() { if (window is not null) { window.Activate(); return; } window = new SettingsWindow(settings, Preview, () => status); window.Closed += (_, _) => window = null; window.Show(); }
        using var menu = new TrayMenu(settings);
        menu.AddCommand("Settings", OpenSettings);
        menu.AddCommand("Preview overlay", Preview);
        menu.AddCommand("Open diagnostics folder", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", Path.GetDirectoryName(Settings.FilePath)!) { UseShellExecute = true }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.AddCommand("Exit", () => app.Shutdown());
        tray.ContextMenuStrip = menu;
        tray.MouseUp += (_, e) => { if (e.Button == Forms.MouseButtons.Left) app.Dispatcher.Invoke(OpenSettings); };
        Directory.CreateDirectory(Path.GetDirectoryName(Settings.FilePath)!);
        void Log(string line)
        {
            try { var path = Path.Combine(Path.GetDirectoryName(Settings.FilePath)!, "diagnostics.log"); if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true); File.AppendAllText(path, DateTimeOffset.Now.ToString("o") + " " + line + Environment.NewLine); } catch { }
        }
        observer.Status += value => app.Dispatcher.BeginInvoke(new Action(() => { status = value; tray.Text = value.StartsWith("Connected") ? "ChatMix Overlay — observing Sonar" : "ChatMix Overlay — reconnecting"; Log(value); }));
        observer.Reading += (value, state, source) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            var old = previous; previous = value;
            if (source == "baseline") { Log($"Baseline {value:F4} {state}"); return; }
            if (old.HasValue && Math.Abs(old.Value - value) > .001) { Log($"{source} balance={value:F4} state={state}"); Present(value); }
        }));
        var worker = observer.RunAsync();
        using var lifetime = new CancellationTokenSource();
        var commands = SingleInstance.Listen(command => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (command == "exit") app.Shutdown();
            else if (command == "settings") OpenSettings();
            else { using var process = System.Diagnostics.Process.GetCurrentProcess(); Log($"Memory privateMB={process.PrivateMemorySize64 / 1048576.0:F1} workingSetMB={process.WorkingSet64 / 1048576.0:F1} managedMB={GC.GetTotalMemory(false) / 1048576.0:F1}"); }
        })), lifetime.Token);
        app.Exit += (_, _) => { lifetime.Cancel(); observer.Dispose(); tray.Visible = false; overlay?.Close(); };
        app.Startup += (_, _) =>
        {
            try { WindowsIntegration.Initialize(settings); } catch (Exception ex) { Log("Windows shortcuts: " + ex.Message); }
            if (args.Contains("--demo")) Preview();
            if (args.Contains("--settings")) OpenSettings();
        };
        return app.Run();
    }
    private static int RenderPreview()
    {
        var app = new System.Windows.Application();
        var settings = new Settings();
        var overlay = new Overlay(settings);
        overlay.Present(.45);
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            void Export(FrameworkElement element, string name, int width, int height)
            {
                element.Measure(new System.Windows.Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(AppContext.BaseDirectory, name)); png.Save(output);
            }
            Export((FrameworkElement)overlay.Content, "preview-windows.png", 272, 76);
            var window = new SettingsWindow(settings, () => { }, () => "Connected to Sonar");
            Export((FrameworkElement)window.Content, "preview-settings.png", 480, 590);
            window.Close();
            settings.ColorMode = "Dark"; overlay.Present(.45);
            Export((FrameworkElement)overlay.Content, "preview-dark.png", 272, 76);
            var darkWindow = new SettingsWindow(settings, () => { }, () => "Connected to Sonar");
            Export((FrameworkElement)darkWindow.Content, "preview-settings-dark.png", 480, 590);
            darkWindow.Close();
            overlay.Close(); app.Shutdown();
        };
        timer.Start(); return app.Run();
    }
    private static int SelfTest()
    {
        bool Parse(string json, double expected) { using var d = System.Text.Json.JsonDocument.Parse(json); return SonarObserver.TryBalance(d.RootElement, out var value, out _) && Math.Abs(value - expected) < .00001; }
        bool Reject(string json) { using var d = System.Text.Json.JsonDocument.Parse(json); return !SonarObserver.TryBalance(d.RootElement, out _, out _); }
        var ok = Parse("{\"balance\":0.009999997,\"state\":\"finiteWheel\"}", .01) && Parse("{\"balance\":-1}", -1) && Parse("{\"balance\":1}", 1) && Reject("{}") && Reject("{\"balance\":2}") && Reject("{\"balance\":\"0.2\"}");
        try { SonarObserver.LocalAddress("http://example.com:80", "http"); ok = false; } catch (InvalidDataException) { }
        var temp = Path.Combine(Path.GetTempPath(), "ChatMixOverlay-test-" + Guid.NewGuid().ToString("N"));
        var shortcut = Path.Combine(temp, "ChatMix Overlay.lnk");
        try
        {
            WindowsIntegration.EnsureShortcut(shortcut, Environment.ProcessPath!, "--settings");
            var modified = File.GetLastWriteTimeUtc(shortcut);
            WindowsIntegration.EnsureShortcut(shortcut, Environment.ProcessPath!, "--settings");
            ok &= File.GetLastWriteTimeUtc(shortcut) == modified && Directory.GetFiles(temp).Length == 1;
            WindowsIntegration.EnsureShortcut(shortcut, Environment.ProcessPath!, "--background");
            var updated = File.GetLastWriteTimeUtc(shortcut);
            WindowsIntegration.EnsureShortcut(shortcut, Environment.ProcessPath!, "--background");
            ok &= File.GetLastWriteTimeUtc(shortcut) == updated && Directory.GetFiles(temp).Length == 1;
        }
        catch { ok = false; }
        finally { if (File.Exists(shortcut)) File.Delete(shortcut); if (Directory.Exists(temp)) Directory.Delete(temp); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.txt"), ok ? "PASS: observed payload, bounds, malformed data, loopback restriction, shortcut creation/update/idempotence" : "FAIL");
        return ok ? 0 : 1;
    }
}
