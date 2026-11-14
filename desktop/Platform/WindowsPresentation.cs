using System.ComponentModel;
using System.Runtime.InteropServices;
namespace WirePulse;

public static partial class DesktopHost {
 const int ClassStyle=-26;const long OwnOrClassDc=0x60,Layered=0x80000;
 [DllImport("user32.dll",EntryPoint="GetClassLongPtrW",SetLastError=true)]static extern IntPtr GetClassLongPtr(IntPtr window,int index);
 [DllImport("user32.dll",EntryPoint="SetClassLongPtrW",SetLastError=true)]static extern IntPtr SetClassLongPtr(IntPtr window,int index,IntPtr value);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr window);
 [DllImport("user32.dll",SetLastError=true)]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
 [DllImport("user32.dll")]static extern bool RedrawWindow(IntPtr window,IntPtr rect,IntPtr region,uint flags);
 public static bool RedirectionRenderer()=>GraphicsBackend.Observed?GraphicsBackend.PlatformBackend is "CPU (compatibilidad)" or "OpenGL":GraphicsBackend.Requested is "CPU (compatibilidad)" or "OpenGL";
 internal static long LayerCompatibleClassStyle(long style)=>style&~OwnOrClassDc;
 internal static void AttachWindowsSurface(IntPtr window,IntPtr parent,IntPtr icons,bool raised,int width,int height,int x,int y,bool redirected,bool legacyClassStyle=false){
  long style=GetWindowLongPtr(window,-16).ToInt64();SetWindowLongPtr(window,-16,new IntPtr((style&~0x80000000L)|0x40000000));
  if(raised&&redirected&&!legacyClassStyle)PrepareLayeredClass(window);
  long ex=GetWindowLongPtr(window,-20).ToInt64();SetWindowLongPtr(window,-20,new IntPtr((ex&~0x40000L)|0x80|0x20|0x08000000|(raised?Layered:0)));
  Marshal.SetLastPInvokeError(0);var previous=SetParent(window,parent);if(previous==IntPtr.Zero&&Marshal.GetLastPInvokeError()!=0)throw new Win32Exception(Marshal.GetLastPInvokeError(),"No se pudo adjuntar el fondo al escritorio.");
  if(raised){bool alpha=SetLayeredWindowAttributes(window,0,255,2);if(redirected&&!legacyClassStyle&&!alpha)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows no pudo activar la capa visible del fondo.");}
  if(!SetWindowPos(window,raised?icons:IntPtr.Zero,x,y,width,height,0x10|0x20|0x40))throw new Win32Exception(Marshal.GetLastWin32Error(),"No se pudo colocar el fondo.");
 }
 static void PrepareLayeredClass(IntPtr window){long previous=GetClassLongPtr(window,ClassStyle).ToInt64(),next=LayerCompatibleClassStyle(previous);if(previous==next)return;Marshal.SetLastPInvokeError(0);SetClassLongPtr(window,ClassStyle,new IntPtr(next));if((GetClassLongPtr(window,ClassStyle).ToInt64()&OwnOrClassDc)!=0)throw new Win32Exception(Marshal.GetLastPInvokeError(),"La clase de ventana no permite mostrar el fondo por CPU/OpenGL.");}
 // The actual fallback can become known only after the first draw. Repair that window once on the UI thread.
 public static void EnsurePresentation(IntPtr window){if(!OperatingSystem.IsWindows()||!RedirectionRenderer()||window==IntPtr.Zero)return;long ex=GetWindowLongPtr(window,-20).ToInt64();if((ex&Layered)==0||(GetClassLongPtr(window,ClassStyle).ToInt64()&OwnOrClassDc)==0)return;PrepareLayeredClass(window);SetWindowLongPtr(window,-20,new IntPtr(ex&~Layered));SetWindowLongPtr(window,-20,new IntPtr(ex));if(!SetLayeredWindowAttributes(window,0,255,2))throw new Win32Exception(Marshal.GetLastWin32Error(),"No se pudo recuperar la presentación del fondo.");RedrawWindow(window,IntPtr.Zero,IntPtr.Zero,0x1|0x80|0x100);}
 public static object? PresentationInfo(IntPtr window){if(!OperatingSystem.IsWindows()||window==IntPtr.Zero)return null;long cls=GetClassLongPtr(window,ClassStyle).ToInt64(),ex=GetWindowLongPtr(window,-20).ToInt64();bool alphaSet=GetLayeredWindowAttributes(window,out _,out byte alpha,out _);return new{parent=GetParent(window).ToInt64(),classHasOwnDc=(cls&OwnOrClassDc)!=0,layered=(ex&Layered)!=0,alphaConfigured=alphaSet,alpha};}
}
