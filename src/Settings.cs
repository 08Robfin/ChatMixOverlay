using System.IO;
using System.Text.Json;

namespace ChatMixOverlay;
public sealed class Settings
{
    public string ColorMode { get; set; } = "Light";
    public double Duration { get; set; } = 1.5;
    public string Position { get; set; } = "Bottom center";
    public string Monitor { get; set; } = "Primary";
    public bool Reverse { get; set; }
    public bool Startup { get; set; }
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatMixOverlay", "settings.json");
    public static Settings Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new();
            s.Duration = double.IsFinite(s.Duration) ? Math.Clamp(s.Duration, .5, 10) : 1.5;
            return s;
        }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (Startup) key.SetValue("ChatMixOverlay", "\"" + Environment.ProcessPath + "\"");
        else key.DeleteValue("ChatMixOverlay", false);
    }
}
