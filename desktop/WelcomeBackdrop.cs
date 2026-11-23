using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System.Runtime.InteropServices;
namespace WirePulse;

// Cinnamon/X11 has no standard per-window blur request. Freeze and blur only the
// small region behind this short-lived window, once, before it is made visible.
internal static class WelcomeBackdrop
{
 [DllImport("libX11.so.6")]static extern IntPtr XOpenDisplay(IntPtr name);
 [DllImport("libX11.so.6")]static extern int XCloseDisplay(IntPtr display);
 [DllImport("libX11.so.6")]static extern UIntPtr XDefaultRootWindow(IntPtr display);
 [DllImport("libX11.so.6")]static extern IntPtr XGetImage(IntPtr display,UIntPtr window,int x,int y,uint width,uint height,UIntPtr mask,int format);
 [DllImport("libX11.so.6")]static extern UIntPtr XGetPixel(IntPtr image,int x,int y);
 [DllImport("libX11.so.6")]static extern int XDestroyImage(IntPtr image);
 public static Bitmap? Capture(Screen screen,double width,double height)
 {
  if(!OperatingSystem.IsLinux()||string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))return null;
  IntPtr display=IntPtr.Zero,image=IntPtr.Zero;
  try{
   display=XOpenDisplay(IntPtr.Zero);if(display==IntPtr.Zero)return null;
   var bounds=screen.WorkingArea;int w=Math.Min(bounds.Width,(int)(width*screen.Scaling)),h=Math.Min(bounds.Height,(int)(height*screen.Scaling));
   image=XGetImage(display,XDefaultRootWindow(display),Math.Max(0,bounds.X+(bounds.Width-w)/2),Math.Max(0,bounds.Y+(bounds.Height-h)/2),(uint)w,(uint)h,UIntPtr.MaxValue,2);if(image==IntPtr.Zero)return null;
   using var source=new SKBitmap(w,h);unsafe{uint* pixels=(uint*)source.GetPixels();for(int y=0;y<h;y++)for(int x=0;x<w;x++){uint rgb=(uint)XGetPixel(image,x,y).ToUInt64();pixels[y*w+x]=0xff000000|rgb;}}
   using var result=new SKBitmap((int)width,(int)height);using(var canvas=new SKCanvas(result)){canvas.Clear(SKColors.Black);using var paint=new SKPaint{ImageFilter=SKImageFilter.CreateBlur(22,22,SKShaderTileMode.Clamp)};canvas.Scale((float)width/w,(float)height/h);canvas.DrawBitmap(source,0,0,paint);}
   using var png=result.Encode(SKEncodedImageFormat.Png,100);using var stream=png.AsStream();return new Bitmap(stream);
  }catch(Exception e){System.Diagnostics.Trace.WriteLine("Welcome backdrop: "+e.Message);return null;}
  finally{if(image!=IntPtr.Zero)XDestroyImage(image);if(display!=IntPtr.Zero)XCloseDisplay(display);}
 }
}
