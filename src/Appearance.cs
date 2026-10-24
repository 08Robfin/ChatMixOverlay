using Microsoft.Win32;
using System.Windows.Media;
namespace ChatMixOverlay;
public static class Appearance
{
    public static bool IsLight(Settings settings)
    {
        if (settings.ColorMode == "Light") return true;
        if (settings.ColorMode == "Dark") return false;
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return (int?)key?.GetValue("AppsUseLightTheme") != 0; } catch { return true; }
    }
    public static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    public static System.Drawing.Icon LoadIcon() => new(typeof(Appearance).Assembly.GetManifestResourceStream("ChatMixOverlay.app.ico")!);
}
