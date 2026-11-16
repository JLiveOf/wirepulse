using System.ComponentModel;
using System.Runtime.InteropServices;
using SkiaSharp;
namespace WirePulse;

/// Test HWNDs plus an explicit Explorer integration mode for disposable Windows CI accounts.
internal sealed class WindowsDesktopFixture:IDisposable {
 const int X=64,Y=64,W=640,H=360;readonly IntPtr host,worker,icons;readonly bool raised,explorer;readonly IntPtr hiddenProbe;readonly List<(IntPtr Window,Placement Placement)> minimized=[];
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string? title);
 delegate bool EnumProc(IntPtr window,IntPtr arg);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc action,IntPtr arg);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,System.Text.StringBuilder name,int max);
 [StructLayout(LayoutKind.Sequential)]struct Placement{public uint Length,Flags,ShowCommand;public int MinX,MinY,MaxX,MaxY,Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetStyle(IntPtr window,int index);
 [DllImport("user32.dll")]static extern bool GetWindowPlacement(IntPtr window,ref Placement placement);
 [DllImport("user32.dll")]static extern bool SetWindowPlacement(IntPtr window,ref Placement placement);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
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
 public WindowsDesktopFixture(bool raised,bool explorer=false){this.raised=raised;this.explorer=explorer;if(explorer){if(FindWindow("Progman",null)==IntPtr.Zero){System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe"){UseShellExecute=true});for(int i=0;i<100&&FindWindow("Progman",null)==IntPtr.Zero;i++)Thread.Sleep(100);}if(FindWindow("Progman",null)==IntPtr.Zero)throw new InvalidOperationException("Explorer desktop unavailable");hiddenProbe=CreateWindowEx(0,"STATIC","WirePulse hidden-window regression",0,0,0,1,1,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
EnumWindows((window,_)=>{
 if(!IsWindowVisible(window)||IsIconic(window)||(GetStyle(window,-16).ToInt64()&0x00C00000)!=0x00C00000)return true;
 var name=new System.Text.StringBuilder(128);GetClassName(window,name,128);
 if(name.ToString() is not("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32769" or "CodexComputerUseSwiftOverlay")){
  var placement=new Placement{Length=(uint)Marshal.SizeOf<Placement>()};
  if(GetWindowPlacement(window,ref placement)){minimized.Add((window,placement));ShowWindow(window,7);}
 }
 return true;
},IntPtr.Zero);
if(hiddenProbe!=IntPtr.Zero&&IsWindowVisible(hiddenProbe))throw new InvalidOperationException("Desktop test exposed a hidden helper window.");return;}host=CreateWindowEx((raised?0x200000u:0)|0x88,"STATIC","WirePulse desktop presentation fixture",0x96000000,X,Y,W,H,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(host==IntPtr.Zero)throw new Win32Exception();worker=CreateWindowEx(0,"STATIC","Wallpaper underlay",0x56000006,0,0,W,H,host,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);icons=CreateWindowEx(0,"STATIC","Icons marker",0x56000004,0,0,1,1,host,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(worker==IntPtr.Zero||icons==IntPtr.Zero)throw new Win32Exception();}
 public WindowsLayeredPresenter? Attach(IntPtr window,bool legacy){if(explorer){var presenter=DesktopHost.Attach(window,W,H,X,Y);if(legacy){presenter?.Dispose();return null;}return presenter;}return DesktopHost.AttachWindowsSurface(window,host,icons,raised,W,H,0,0,redirected:true,legacyClassStyle:legacy);}
 public (int Matching,int Samples,int Colored,int MediaPixels,bool Upright) Check(string? imagePath){DwmFlush();var screen=GetDC(IntPtr.Zero);var dc=CreateCompatibleDC(screen);var handle=CreateCompatibleBitmap(screen,W,H);var old=SelectObject(dc,handle);try{if(!BitBlt(dc,0,0,W,H,screen,X,Y,0x00CC0020|0x40000000))throw new Win32Exception();SelectObject(dc,old);using var bitmap=new SKBitmap(new SKImageInfo(W,H,SKColorType.Bgra8888,SKAlphaType.Opaque));var info=new BitmapInfo{Size=40,Width=W,Height=-H,Planes=1,Bits=32};if(GetDIBits(dc,handle,0,H,bitmap.GetPixels(),ref info,0)==0)throw new Win32Exception();if(imagePath!=null){using var file=File.Create(imagePath);bitmap.Encode(file,SKEncodedImageFormat.Png,100);}int matching=0,samples=0;foreach(int y in new[]{50,100,200})foreach(int x in new[]{20,100,200}){var pixel=bitmap.GetPixel(x,y);samples++;if(Math.Abs(pixel.Red-24)<8&&Math.Abs(pixel.Green-54)<8&&Math.Abs(pixel.Blue-82)<8)matching++;}int colored=0,mediaPixels=0;for(int y=0;y<H;y++)for(int x=0;x<W;x++){var pixel=bitmap.GetPixel(x,y);int highest=Math.Max(pixel.Red,Math.Max(pixel.Green,pixel.Blue)),lowest=Math.Min(pixel.Red,Math.Min(pixel.Green,pixel.Blue));if(highest>100&&highest>lowest*1.5)mediaPixels++;if(pixel.Red>100&&pixel.Red>pixel.Blue*1.3&&pixel.Red>pixel.Green*1.3)colored++;}var top=bitmap.GetPixel(128,72);var bottom=bitmap.GetPixel(480,270);bool upright=top.Red>180&&top.Green<80&&top.Blue<80&&bottom.Green>180&&bottom.Red<80&&bottom.Blue<80;return(matching,samples,colored,mediaPixels,upright);}finally{SelectObject(dc,old);DeleteObject(handle);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}}
 public void Dispose(){if(!explorer)DestroyWindow(host);if(hiddenProbe!=IntPtr.Zero)DestroyWindow(hiddenProbe);foreach(var item in minimized){var placement=item.Placement;SetWindowPlacement(item.Window,ref placement);}}
}
