using Microsoft.Win32;
using System.Text;
namespace WirePulse;

/// Per-user, opt-in registration. Never writes an all-users startup location.
public sealed class StartupRegistration
{
    const string ValueName = "WirePulse"; readonly bool windows; readonly string executable, folder, registryPath;
    public StartupRegistration(string? executable = null, string? folder = null, string? registryPath = null, bool? windows = null) { this.windows = windows ?? OperatingSystem.IsWindows(); this.executable = executable ?? Environment.ProcessPath ?? throw new InvalidOperationException("No se encuentra el ejecutable"); this.folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart"); this.registryPath = registryPath ?? @"Software\Microsoft\Windows\CurrentVersion\Run"; }
    public bool PriorityEnabled => windows && WindowsStartupTask.Enabled(registryPath);
    public void SetPriority(bool priority) { if (!windows) throw new PlatformNotSupportedException(); if (priority) { WindowsStartupTask.Register(executable, registryPath); using var key = Registry.CurrentUser.OpenSubKey(registryPath, true); key?.DeleteValue(ValueName, false); } else { SetEnabled(true); } }
    public bool Enabled { get { if (windows) { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException(); using var key = Registry.CurrentUser.OpenSubKey(registryPath); return (key?.GetValue(ValueName) is string value && value.Contains("--autostart", StringComparison.Ordinal)) || PriorityEnabled; } var path = Path.Combine(folder, "wirepulse.desktop"); if (!File.Exists(path)) return false; var entry = File.ReadAllText(path); return entry.Contains("X-WirePulse-Managed=true") && !entry.Contains("Hidden=true") && !entry.Contains("X-GNOME-Autostart-enabled=false"); } }
    public void SetEnabled(bool enabled)
    {
        if (windows) { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException(); if (enabled) { using var key = Registry.CurrentUser.CreateSubKey(registryPath); key.SetValue(ValueName, WindowsCommand(executable), RegistryValueKind.String); WindowsStartupTask.Remove(registryPath); } else { using var key = Registry.CurrentUser.OpenSubKey(registryPath, true); key?.DeleteValue(ValueName, false); WindowsStartupTask.Remove(registryPath); } return; }
        var path = Path.Combine(folder, "wirepulse.desktop"); if (enabled) { Directory.CreateDirectory(folder); File.WriteAllText(path, LinuxEntry(executable), new UTF8Encoding(false)); if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } else if (File.Exists(path)) File.Delete(path);
    }
    public void RemoveOwned()
    {
        if (!windows) return;
        if (WindowsStartupTask.OwnedBy(executable, registryPath)) WindowsStartupTask.Remove(registryPath);
        using var key=Registry.CurrentUser.OpenSubKey(registryPath, true);
        if (string.Equals(key?.GetValue(ValueName) as string, WindowsCommand(executable), StringComparison.OrdinalIgnoreCase)) key?.DeleteValue(ValueName, false);
    }
    public static string WindowsCommand(string path) => "\"" + path + "\" --autostart --no-settings";
    static string EntryString(string text) => text.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
    public static string LinuxEntry(string path) { var quoted = new StringBuilder("\""); foreach (char c in path) { if (c is '\\' or '"' or '$' or '`') quoted.Append('\\'); quoted.Append(c == '%' ? "%%" : c.ToString()); } quoted.Append('"'); return "[Desktop Entry]\nType=Application\nName=WirePulse\nComment=Fondo reactivo al audio\nExec=" + EntryString(quoted.ToString()) + " --autostart --no-settings\nTryExec=" + EntryString(path) + "\nTerminal=false\nX-GNOME-Autostart-enabled=true\nX-WirePulse-Managed=true\n"; }
    public static bool ShouldPrompt(bool asked, bool autostart, bool suppressed) => !asked && !autostart && !suppressed;
}
