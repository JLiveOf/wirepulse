using System.ComponentModel;
using System.Runtime.InteropServices;
using SkiaSharp;
namespace WirePulse;

/// Owns only its test windows. Exercises the raised-desktop hierarchy without modifying Explorer.
internal sealed class WindowsDesktopFixture:IDisposable {
 const int X=64,Y=64,W=640,H=360;readonly IntPtr host,worker,icons;readonly bool raised;
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
 [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool BitBlt(IntPtr dest,int x,int y,int width,int height,IntPtr source,int sx,int sy,uint flags);
 [DllImport("gdi32.dll")]static extern int GetDIBits(IntPtr dc,IntPtr bitmap,uint start,uint lines,IntPtr pixels,ref BitmapInfo info,uint usage);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 [DllImport("dwmapi.dll")]static extern int DwmFlush();
 [StructLayout(LayoutKind.Sequential)]struct BitmapInfo{public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int Xppm,Yppm;public uint Used,Important;}
 public WindowsDesktopFixture(bool raised){this.raised=raised;host=CreateWindowEx((raised?0x200000u:0)|0x88,"STATIC","WirePulse desktop presentation fixture",0x90000000,X,Y,W,H,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(host==IntPtr.Zero)throw new Win32Exception();worker=CreateWindowEx(0,"STATIC","Wallpaper underlay",0x50000006,0,0,W,H,host,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);icons=CreateWindowEx(0,"STATIC","Icons marker",0x50000004,0,0,1,1,host,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(worker==IntPtr.Zero||icons==IntPtr.Zero)throw new Win32Exception();}
 public void Attach(IntPtr window,bool legacy){DesktopHost.AttachWindowsSurface(window,host,icons,raised,W,H,0,0,redirected:true,legacyClassStyle:legacy);}
 public (int Matching,int Samples) Check(string? imagePath){DwmFlush();var screen=GetDC(IntPtr.Zero);var dc=CreateCompatibleDC(screen);var handle=CreateCompatibleBitmap(screen,W,H);var old=SelectObject(dc,handle);try{if(!BitBlt(dc,0,0,W,H,screen,X,Y,0x00CC0020|0x40000000))throw new Win32Exception();using var bitmap=new SKBitmap(new SKImageInfo(W,H,SKColorType.Bgra8888,SKAlphaType.Opaque));var info=new BitmapInfo{Size=40,Width=W,Height=-H,Planes=1,Bits=32};if(GetDIBits(dc,handle,0,H,bitmap.GetPixels(),ref info,0)==0)throw new Win32Exception();if(imagePath!=null){using var file=File.Create(imagePath);bitmap.Encode(file,SKEncodedImageFormat.Png,100);}int matching=0,samples=0;foreach(int y in new[]{50,100,200})foreach(int x in new[]{20,100,200}){var pixel=bitmap.GetPixel(x,y);samples++;if(Math.Abs(pixel.Red-24)<8&&Math.Abs(pixel.Green-54)<8&&Math.Abs(pixel.Blue-82)<8)matching++;}return(matching,samples);}finally{SelectObject(dc,old);DeleteObject(handle);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}}
 public void Dispose(){DestroyWindow(host);}
}
