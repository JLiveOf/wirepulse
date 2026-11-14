using System.ComponentModel;
using System.Runtime.InteropServices;
namespace WirePulse;

public static partial class DesktopHost {
 const long OwnOrClassDc=0x60,Layered=0x80000;
 [DllImport("user32.dll",EntryPoint="GetClassLongPtrW",SetLastError=true)]static extern IntPtr GetClassLongPtr(IntPtr window,int index);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
 [DllImport("user32.dll",SetLastError=true)]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rectangle rect);
 [StructLayout(LayoutKind.Sequential)]struct Rectangle{public int Left,Top,Right,Bottom;}
 public static bool RedirectionRenderer()=>GraphicsBackend.Observed?GraphicsBackend.PlatformBackend is "CPU (compatibilidad)" or "OpenGL":GraphicsBackend.Requested is "CPU (compatibilidad)" or "OpenGL";
 internal static WindowsLayeredPresenter? AttachWindowsSurface(IntPtr window,IntPtr parent,IntPtr icons,bool raised,int width,int height,int x,int y,bool redirected,bool legacyClassStyle=false){
  long style=GetWindowLongPtr(window,-16).ToInt64();SetWindowLongPtr(window,-16,new IntPtr((style&~0x80000000L)|0x40000000));
  long ex=GetWindowLongPtr(window,-20).ToInt64();bool separate=raised&&redirected&&!legacyClassStyle;long hostStyle=(ex&~0x40000L)|0x80|0x20|0x08000000;hostStyle=separate?hostStyle&~Layered:hostStyle|(raised?Layered:0);SetWindowLongPtr(window,-20,new IntPtr(hostStyle));
  Marshal.SetLastPInvokeError(0);var previous=SetParent(window,parent);if(previous==IntPtr.Zero&&Marshal.GetLastPInvokeError()!=0)throw new Win32Exception(Marshal.GetLastPInvokeError(),"No se pudo adjuntar el fondo al escritorio.");
  if(raised&&!separate)SetLayeredWindowAttributes(window,0,255,2);
  if(!SetWindowPos(window,raised?icons:IntPtr.Zero,x,y,width,height,0x10|0x20|0x40))throw new Win32Exception(Marshal.GetLastWin32Error(),"No se pudo colocar el fondo.");
  return raised&&redirected&&!legacyClassStyle?new WindowsLayeredPresenter(parent,icons,width,height,x,y):null;
 }
 // CPU can be chosen as an automatic fallback only after the first draw. Add its presenter on the UI thread.
 public static WindowsLayeredPresenter? EnsurePresentation(IntPtr window){if(!OperatingSystem.IsWindows()||!RedirectionRenderer()||window==IntPtr.Zero)return null;var parent=GetParent(window);if(parent==IntPtr.Zero||(GetWindowLongPtr(parent,-20).ToInt64()&0x200000)==0)return null;SetWindowLongPtr(window,-20,new IntPtr(GetWindowLongPtr(window,-20).ToInt64()&~Layered));GetWindowRect(window,out var rect);var point=new XY{X=rect.Left,Y=rect.Top};ScreenToClient(parent,ref point);var icons=FindWindowEx(parent,IntPtr.Zero,"SHELLDLL_DefView",null);return new WindowsLayeredPresenter(parent,icons,rect.Right-rect.Left,rect.Bottom-rect.Top,point.X,point.Y);}
 public static object? PresentationInfo(IntPtr window){if(!OperatingSystem.IsWindows()||window==IntPtr.Zero)return null;long cls=GetClassLongPtr(window,-26).ToInt64(),ex=GetWindowLongPtr(window,-20).ToInt64();bool alphaSet=GetLayeredWindowAttributes(window,out _,out byte alpha,out _);GetWindowRect(window,out var rect);return new{left=rect.Left,top=rect.Top,right=rect.Right,bottom=rect.Bottom,parent=GetParent(window).ToInt64(),classHasOwnDc=(cls&OwnOrClassDc)!=0,layered=(ex&Layered)!=0,alphaConfigured=alphaSet,alpha};}
}
