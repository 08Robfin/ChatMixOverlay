using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;

namespace ChatMixOverlay;

public static class WindowsIntegration
{
    private const string ShortcutName = "ChatMix Overlay.lnk";
    public static string AppShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ShortcutName);
    public static string StartupShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);
    private static string Executable => Environment.ProcessPath ?? throw new IOException("Cannot locate this application.");

    public static void Initialize(Settings settings)
    {
        EnsureShortcut(AppShortcut, Executable, "--settings");
        // Migrate the old Run entry; never register both startup mechanisms.
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (run?.GetValue("ChatMixOverlay") is string oldCommand && oldCommand.Contains("ChatMixOverlay.exe", StringComparison.OrdinalIgnoreCase))
        {
            EnsureShortcut(StartupShortcut, Executable, "--background");
            run.DeleteValue("ChatMixOverlay", false);
        }
        // Respect shortcuts removed by the user rather than recreating them on every launch.
        settings.Startup = File.Exists(StartupShortcut);
        if (settings.Startup) EnsureShortcut(StartupShortcut, Executable, "--background");
    }

    public static void SetStartup(bool enabled)
    {
        if (enabled) EnsureShortcut(StartupShortcut, Executable, "--background");
        else if (File.Exists(StartupShortcut)) File.Delete(StartupShortcut);
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        run?.DeleteValue("ChatMixOverlay", false);
    }

    public static void EnsureShortcut(string shortcutPath, string executable, string arguments)
    {
        var shellLink = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
        try
        {
            var file = (IPersistFile)shellLink;
            if (File.Exists(shortcutPath))
            {
                try
                {
                    file.Load(shortcutPath, 0);
                    var target = new StringBuilder(32768); var args = new StringBuilder(1024);
                    shellLink.GetPath(target, target.Capacity, IntPtr.Zero, 0);
                    shellLink.GetArguments(args, args.Capacity);
                    if (string.Equals(target.ToString(), executable, StringComparison.OrdinalIgnoreCase) && args.ToString() == arguments) return;
                }
                catch (COMException) { /* Repair a damaged shortcut at the same stable path. */ }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
            shellLink.SetPath(executable);
            shellLink.SetArguments(arguments);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(executable)!);
            shellLink.SetDescription("Show your SteelSeries Sonar Game/Chat balance.");
            shellLink.SetIconLocation(executable, 0);
            shellLink.SetShowCmd(1);
            file.Save(shortcutPath, true);
        }
        finally { Marshal.FinalReleaseComObject(shellLink); }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int count, IntPtr data, uint flags);
        void GetIDList(out IntPtr id); void SetIDList(IntPtr id);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetHotkey(out short key); void SetHotkey(short key);
        void GetShowCmd(out int command); void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string text, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string text, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string text);
    }
}
