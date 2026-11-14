using System.Runtime.InteropServices;
namespace WirePulse;

public static class DesktopHost
{
    public static void Attach(IntPtr window, int width, int height, int x, int y) { if (OperatingSystem.IsWindows()) Windows(window, width, height, x, y); else if (OperatingSystem.IsLinux()) Linux(window); }
    public static void Validate() { if (OperatingSystem.IsLinux() && (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland" || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))) throw new InvalidOperationException("El fondo necesita la sesión Cinnamon (X11). Cierra sesión y elige Cinnamon, no Cinnamon Wayland."); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
    delegate bool EnumProc(IntPtr h, IntPtr p); [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr p);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    public static bool Alive(IntPtr h) => !OperatingSystem.IsWindows() || IsWindow(h);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref XY p);
    [StructLayout(LayoutKind.Sequential)] struct XY { public int X, Y; }
    static void Windows(IntPtr window, int w, int h, int x, int y)
    {
        var prog = FindWindow("Progman", null); if (prog == IntPtr.Zero) throw new Exception("No se encuentra el escritorio de Explorer."); SendMessageTimeout(prog, 0x052C, new IntPtr(0xD), new IntPtr(1), 0, 2000, out _); IntPtr worker = IntPtr.Zero;
        EnumWindows((top, _) => { if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null); return true; }, IntPtr.Zero);
        bool raised = (GetWindowLongPtr(prog, -20).ToInt64() & 0x00200000) != 0; var icons = FindWindowEx(prog, IntPtr.Zero, "SHELLDLL_DefView", null); if (raised) worker = FindWindowEx(prog, IntPtr.Zero, "WorkerW", null); if (worker == IntPtr.Zero) worker = FindWindowEx(prog, IntPtr.Zero, "WorkerW", null); if (worker == IntPtr.Zero) throw new Exception("Explorer no proporciona una superficie WorkerW para el fondo.");
        var parent = raised ? prog : worker; long style = GetWindowLongPtr(window, -16).ToInt64(); SetWindowLongPtr(window, -16, new IntPtr((style & ~unchecked((long)0x80000000)) | 0x40000000)); long ex = GetWindowLongPtr(window, -20).ToInt64(); SetWindowLongPtr(window, -20, new IntPtr((ex & ~0x40000) | 0x80 | 0x20 | 0x08000000 | (raised ? 0x80000L : 0L))); Marshal.SetLastPInvokeError(0); var previous = SetParent(window, parent); if (previous == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0) throw new Exception("No se pudo adjuntar la ventana al escritorio."); var point = new XY { X = x, Y = y }; ScreenToClient(parent, ref point); if (raised) SetLayeredWindowAttributes(window, 0, 255, 2); SetWindowPos(window, raised ? icons : IntPtr.Zero, point.X, point.Y, w, h, 0x10 | 0x20 | 0x40);
    }
    [DllImport("libX11.so.6")] static extern IntPtr XOpenDisplay(IntPtr name); [DllImport("libX11.so.6")] static extern IntPtr XInternAtom(IntPtr d, string name, bool only);
    [DllImport("libX11.so.6")] static extern int XChangeProperty(IntPtr d, IntPtr w, IntPtr property, IntPtr type, int format, int mode, IntPtr[] data, int n);
    [DllImport("libX11.so.6")] static extern int XLowerWindow(IntPtr d, IntPtr w); [DllImport("libX11.so.6")] static extern int XFlush(IntPtr d); [DllImport("libX11.so.6")] static extern int XCloseDisplay(IntPtr d);
    [DllImport("libXfixes.so.3")] static extern IntPtr XFixesCreateRegion(IntPtr d, IntPtr rect, int n); [DllImport("libXfixes.so.3")] static extern void XFixesSetWindowShapeRegion(IntPtr d, IntPtr w, int kind, int x, int y, IntPtr region); [DllImport("libXfixes.so.3")] static extern void XFixesDestroyRegion(IntPtr d, IntPtr region);
    static void Linux(IntPtr window)
    {
        var d = XOpenDisplay(IntPtr.Zero); if (d == IntPtr.Zero) throw new Exception("No se puede conectar con X11."); try
        {
            var atom = XInternAtom(d, "ATOM", false); void Set(string key, params IntPtr[] data) => XChangeProperty(d, window, XInternAtom(d, key, false), atom, 32, 0, data, data.Length);
            Set("_NET_WM_WINDOW_TYPE", XInternAtom(d, "_NET_WM_WINDOW_TYPE_DESKTOP", false)); Set("_NET_WM_STATE", XInternAtom(d, "_NET_WM_STATE_BELOW", false), XInternAtom(d, "_NET_WM_STATE_STICKY", false), XInternAtom(d, "_NET_WM_STATE_SKIP_TASKBAR", false), XInternAtom(d, "_NET_WM_STATE_SKIP_PAGER", false)); XChangeProperty(d, window, XInternAtom(d, "_NET_WM_DESKTOP", false), XInternAtom(d, "CARDINAL", false), 32, 0, [new IntPtr(unchecked((long)0xffffffff))], 1); var region = XFixesCreateRegion(d, IntPtr.Zero, 0); XFixesSetWindowShapeRegion(d, window, 2, 0, 0, region); XFixesDestroyRegion(d, region); XLowerWindow(d, window); XFlush(d);
        }
        finally { XCloseDisplay(d); }
    }
}
