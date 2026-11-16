using System.Runtime.InteropServices;
using Avalonia;
namespace WirePulse;

public static class DisplayTiming
{
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string device, int mode, [In, Out] byte[] devMode);
    public static double RefreshRate(PixelRect bounds)
    {
        if (OperatingSystem.IsLinux()) return LinuxRefresh(bounds); if (!OperatingSystem.IsWindows()) return 60; try
        {
            var monitor = MonitorFromPoint(new() { X = bounds.X + bounds.Width / 2, Y = bounds.Y + bounds.Height / 2 }, 2); var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" }; if (!GetMonitorInfo(monitor, ref info)) return 60;
            // DEVMODEW has a 220-byte fixed portion; dmSize at 68, dmDisplayFrequency at 184.
            var mode = new byte[220]; BitConverter.GetBytes((ushort)mode.Length).CopyTo(mode, 68); if (EnumDisplaySettings(info.Device, -1, mode)) { int hz = BitConverter.ToInt32(mode, 184); if (hz is >= 20 and <= 1000) return hz; }
        }
        catch { }
        return 60;
    }
    static Dictionary<PixelRect, double> linux = []; static DateTime linuxRead = DateTime.MinValue;
    static double LinuxRefresh(PixelRect bounds) { if ((DateTime.UtcNow - linuxRead).TotalSeconds > 2) { linuxRead = DateTime.UtcNow; try { using var p = new System.Diagnostics.Process { StartInfo = new() { FileName = "xrandr", Arguments = "--current", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } }; p.Start(); var output = p.StandardOutput.ReadToEndAsync(); _ = p.StandardError.ReadToEndAsync(); if (!p.WaitForExit(750)) { p.Kill(true); return 60; } PixelRect? active = null; var rates = new Dictionary<PixelRect, double>(); foreach (var line in output.GetAwaiter().GetResult().Split('\n')) { var m = System.Text.RegularExpressions.Regex.Match(line, @"^\S+ connected (?:primary )?(\d+)x(\d+)([+-]\d+)([+-]\d+)"); if (m.Success) active = new PixelRect(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)); else if (!line.StartsWith(' ')) active = null; else if (active is PixelRect rect) { var hz = System.Text.RegularExpressions.Regex.Match(line, @"(\d+(?:\.\d+)?)\*"); if (hz.Success && double.TryParse(hz.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && value >= 20) rates[rect] = value; } } linux = rates; } catch { } } return linux.TryGetValue(bounds, out var rate) ? rate : 60; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback,IntPtr parameter);
    delegate bool EnumWindow(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetExtendedStyle(IntPtr window,int index);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
    public static string CoverageReason(PixelRect bounds,bool fullscreen,bool maximized){
        if(!OperatingSystem.IsWindows()||(!fullscreen&&!maximized))return "";
        string reason="";var monitor=MonitorFromPoint(new(){X=bounds.X+bounds.Width/2,Y=bounds.Y+bounds.Height/2},2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>(),Device=""};if(!GetMonitorInfo(monitor,ref info))return "";
        EnumWindows((window,_)=>{if(!IsWindowVisible(window)||IsIconic(window))return true;long style=GetExtendedStyle(window,-20).ToInt64();if((style&(0x20L|0x80L|0x08000000L))!=0)return true;GetWindowThreadProcessId(window,out uint pid);if(pid==Environment.ProcessId)return true;var name=new System.Text.StringBuilder(128);GetClassName(window,name,128);if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32769")return true;if(DwmGetWindowAttribute(window,14,out int cloaked,4)==0&&cloaked!=0)return true;if(!GetWindowRect(window,out var r))return true;var rect=new PixelRect(r.Left,r.Top,Math.Max(0,r.Right-r.Left),Math.Max(0,r.Bottom-r.Top));reason=Coverage(bounds,new PixelRect(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top),rect,MonitorFromWindow(window,2)==monitor,IsZoomed(window),fullscreen,maximized);return reason.Length==0;},IntPtr.Zero);return reason;
    }
    public static string Coverage(PixelRect bounds,PixelRect work,PixelRect window,bool sameMonitor,bool maximized,bool pauseFullscreen,bool pauseMaximized){
        static bool Contains(PixelRect outer,PixelRect inner,int tolerance)=>outer.X<=inner.X+tolerance&&outer.Y<=inner.Y+tolerance&&outer.Right>=inner.Right-tolerance&&outer.Bottom>=inner.Bottom-tolerance;
        if(maximized)return pauseMaximized&&sameMonitor&&Contains(window,work,2)?"ventana maximizada":"";
        if(pauseFullscreen&&Contains(window,bounds,1))return "pantalla completa";
        return "";
    }
    public static bool Covered(PixelRect bounds)=>CoverageReason(bounds,true,false).Length>0;
}
