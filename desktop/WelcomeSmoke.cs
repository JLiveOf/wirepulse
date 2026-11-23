using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
namespace WirePulse;
internal static class WelcomeSmoke
{
 public static void Run(IClassicDesktopStyleApplicationLifetime life){
  var background=new Window{Width=1100,Height=900,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Brushes.Red};background.Show();
  var wait=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};wait.Tick+=(_,_)=>{wait.Stop();try{
   if(OperatingSystem.IsLinux()){
    using var capture=WelcomeBackdrop.Capture(background.Screens.Primary??background.Screens.All.FirstOrDefault()??throw new Exception("No X11 display was reported"),620,640)??throw new Exception("Cinnamon/X11 blur capture failed");using var data=new MemoryStream();capture.Save(data,new Avalonia.Media.Imaging.PngBitmapEncoderOptions());data.Position=0;using var image=SkiaSharp.SKBitmap.Decode(data);var pixel=image.GetPixel(image.Width/2,image.Height/2);if(pixel.Red<150||pixel.Blue>80)throw new Exception("Blur capture lost the desktop's colour channels");
   }
   var window=new WelcomeWindow(false,false,true,"stable",(_,_,_,_)=>throw new Exception("Smoke test must not save preferences"),playSound:false);window.Show();window.SetIntroProgress(.5);if(window.Width!=320||window.Height!=280)throw new Exception("Splash is not compact");window.ShowSetup(animate:false);if(window.Width!=620||window.Height!=640)throw new Exception("Splash did not expand in place");window.Close();background.Close();Console.WriteLine("PASS native welcome window, transparent backdrop, X11 blur pixels and same-window resize");life.Shutdown(0);
  }catch(Exception error){Console.Error.WriteLine(error);life.Shutdown(1);}};wait.Start();
 }
}
