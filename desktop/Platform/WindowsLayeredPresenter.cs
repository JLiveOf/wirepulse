using Avalonia.Threading;
using SkiaSharp;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace WirePulse;

/// A fresh, non-CS_OWNDC HWND presents completed CPU/WGL frames on the raised desktop.
/// The renderer's own DC and WGL context are preserved. Buffers are reused; UI presentation is coalesced.
public sealed class WindowsLayeredPresenter:IDisposable {
 readonly object gate=new();readonly int width,height;readonly IntPtr window;IntPtr dc,bitmap,old,pixels;bool disposed,queued;long frames;double copyMs;string error="";uint firstPixel,samplePixel;
 static readonly WindowProc Procedure=(hwnd,msg,w,l)=>msg==0x84?new IntPtr(-1):DefWindowProc(hwnd,msg,w,l);
 static readonly string ClassName="WirePulse.LayeredPresentation";static ushort atom;
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate IntPtr WindowProc(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct WindowClass{public uint Style;public IntPtr Procedure;public int ClassBytes,WindowBytes;public IntPtr Instance,Icon,Cursor,Brush,Menu;[MarshalAs(UnmanagedType.LPWStr)]public string Name;}
 [StructLayout(LayoutKind.Sequential)]struct Size{public int Width,Height;}
 [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Operation,Flags,Alpha,Format;}
 [StructLayout(LayoutKind.Sequential)]struct BitmapInfo{public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int Xppm,Yppm;public uint Used,Important;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string? name);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern ushort RegisterClass(ref WindowClass cls);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr DefWindowProc(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr dest,IntPtr position,ref Size size,IntPtr source,ref Point sourcePosition,uint key,ref Blend blend,uint flags);
 [DllImport("user32.dll",SetLastError=true)]static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll")]static extern bool BitBlt(IntPtr dest,int x,int y,int width,int height,IntPtr source,int sx,int sy,uint operation);
 [DllImport("gdi32.dll")]static extern bool GdiFlush();
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr pixels,IntPtr section,uint offset);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 public WindowsLayeredPresenter(IntPtr parent,IntPtr icons,int width,int height,int x,int y){this.width=width;this.height=height;if(atom==0){var cls=new WindowClass{Name=ClassName,Instance=GetModuleHandle(null),Procedure=Marshal.GetFunctionPointerForDelegate(Procedure)};atom=RegisterClass(ref cls);if(atom==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"RegisterClass layered presenter failed");}window=CreateWindowEx(0x80000|0x08000000|0x20|0x80,ClassName,"WirePulse desktop presentation",0x54000000,x,y,width,height,parent,IntPtr.Zero,GetModuleHandle(null),IntPtr.Zero);if(window==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"CreateWindowEx layered child failed");if(!SetLayeredWindowAttributes(window,0,255,2))throw new Win32Exception(Marshal.GetLastWin32Error(),"Layered presentation alpha failed");SetWindowPos(window,icons,x,y,width,height,0x10|0x40);}
 public object Info=>new{mode="Capa compatible · GDI",firstPixel,samplePixel,frames=Interlocked.Read(ref frames),copyMs=Volatile.Read(ref copyMs),error=Volatile.Read(ref error),window=DesktopHost.PresentationInfo(window)};
 public long Frames=>Interlocked.Read(ref frames);
 public void Capture(SKSurface surface){lock(gate){if(disposed)return;long start=Stopwatch.GetTimestamp();if(dc==IntPtr.Zero){dc=CreateCompatibleDC(IntPtr.Zero);var info=new BitmapInfo{Size=40,Width=width,Height=-height,Planes=1,Bits=32};bitmap=CreateDIBSection(dc,ref info,0,out pixels,IntPtr.Zero,0);if(dc==IntPtr.Zero||bitmap==IntPtr.Zero){if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(dc!=IntPtr.Zero)DeleteDC(dc);dc=bitmap=pixels=IntPtr.Zero;error="No se pudo reservar el fotograma del fondo.";return;}old=SelectObject(dc,bitmap);}if(!surface.ReadPixels(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Premul),pixels,width*4,0,0)){error="No se pudo leer el fotograma del fondo.";return;}firstPixel=unchecked((uint)Marshal.ReadInt32(pixels));samplePixel=unchecked((uint)Marshal.ReadInt32(pixels,(Math.Min(50,height-1)*width+Math.Min(20,width-1))*4));copyMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;if(!queued){queued=true;Dispatcher.UIThread.Post(Present,DispatcherPriority.Render);}}}
 void Present(){lock(gate){queued=false;if(disposed||bitmap==IntPtr.Zero)return;var target=GetDC(window);try{if(target!=IntPtr.Zero&&BitBlt(target,0,0,width,height,dc,0,0,0x00CC0020)){GdiFlush();Interlocked.Increment(ref frames);error="";}else error="No se pudo presentar el fotograma: "+Marshal.GetLastWin32Error();}finally{if(target!=IntPtr.Zero)ReleaseDC(window,target);}}}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;if(dc!=IntPtr.Zero){SelectObject(dc,old);DeleteObject(bitmap);DeleteDC(dc);}DestroyWindow(window);}}
}
