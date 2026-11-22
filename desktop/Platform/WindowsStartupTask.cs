using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
namespace WirePulse;

/// Login scheduling only: no elevation, credentials, or CPU-priority changes.
internal static class WindowsStartupTask
{
    static string Name(string registryPath) => "WirePulse-Login-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(WindowsIdentity.GetCurrent().User!.Value + registryPath)))[..16];
    static T WithFolder<T>(Func<dynamic,T> action)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
        object? folder = null;
        try { service.Connect(); folder = service.GetFolder("\\"); return action(folder); }
        finally { if (folder != null) Marshal.FinalReleaseComObject(folder); Marshal.FinalReleaseComObject(service); }
    }
    public static bool Enabled(string key) => WithFolder<bool>(folder => {
        object? task = null;
        try { task = folder.GetTask(Name(key)); return ((dynamic)task).Enabled; }
        catch (COMException e) when ((uint)e.HResult == 0x80070002) { return false; }
        finally { if (task != null) Marshal.FinalReleaseComObject(task); }
    });
    public static bool OwnedBy(string executable, string key) => WithFolder<bool>(folder => {
        object? task=null;
        try { task=folder.GetTask(Name(key)); var xml=XDocument.Parse((string)((dynamic)task).Xml); XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task"; return string.Equals(xml.Descendants(ns+"Command").FirstOrDefault()?.Value, executable, StringComparison.OrdinalIgnoreCase); }
        catch(COMException e) when ((uint)e.HResult==0x80070002) { return false; }
        finally { if(task!=null) Marshal.FinalReleaseComObject(task); }
    });
    public static void Remove(string key) => WithFolder<bool>(folder => {
        try { folder.DeleteTask(Name(key), 0); }
        catch (COMException e) when ((uint)e.HResult == 0x80070002) { }
        return true;
    });
    public static void Register(string executable, string key) => WithFolder<bool>(folder => {
        var sid = WindowsIdentity.GetCurrent().User!.Value;
        object task = folder.RegisterTask(Name(key), Xml(executable, sid), 6, sid, null, 3, null);
        Marshal.FinalReleaseComObject(task); return true;
    });
    internal static string Xml(string executable, string sid)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, object content) => new(ns + name, content);
        return new XElement(ns + "Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Description", "WirePulse: inicio prioritario por usuario")),
            E("Triggers", E("LogonTrigger", new object[]{ E("Enabled", "true"), E("UserId", sid) })),
            E("Principals", new XElement(ns + "Principal", new XAttribute("id", "User"), E("UserId", sid), E("LogonType", "InteractiveToken"), E("RunLevel", "LeastPrivilege"))),
            E("Settings", new object[]{ E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", "false"), E("StopIfGoingOnBatteries", "false"), E("ExecutionTimeLimit", "PT0S"), E("Priority", "7") }),
            new XElement(ns + "Actions", new XAttribute("Context", "User"), E("Exec", new object[]{ E("Command", executable), E("Arguments", "--autostart --no-settings"), E("WorkingDirectory", Path.GetDirectoryName(executable) ?? "") }))).ToString();
    }
}
