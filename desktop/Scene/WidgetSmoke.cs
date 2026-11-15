using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SkiaSharp;
namespace WirePulse;
internal static class WidgetSmoke {
 public static void Run(IClassicDesktopStyleApplicationLifetime life){IWidgetBrowser? browser=null;DispatcherTimer? timer=null;var start=DateTime.UtcNow;bool busy=false,painted=false;var window=new Window{Title="WirePulse widget integration",Width=400,Height=240,Position=new Avalonia.PixelPoint(-10000,-10000),ShowInTaskbar=false};life.MainWindow=window;window.Show();try{
 string path=Path.GetFullPath(Program.Option("--widget-smoke-test")!);
#if WINDOWS
 browser=new WindowsWidgetBrowser(path);
#else
 browser=new GtkWidgetBrowser(path);
#endif
 timer=new DispatcherTimer(TimeSpan.FromMilliseconds(150),DispatcherPriority.Background,async(_,_)=>{if(busy)return;busy=true;try{browser.Audio(System.Text.Json.JsonSerializer.Serialize(new{bands=new double[64],beat=.8,bpm=painted?123.45:120d,playing=true}));var bytes=await browser.Snapshot(400,240);if(bytes!=null){using var bitmap=SKBitmap.Decode(bytes);var p=bitmap.GetPixel(200,120);if(!painted&&Math.Abs(p.Red-25)<8&&Math.Abs(p.Green-160)<8&&Math.Abs(p.Blue-224)<8){painted=true;Console.WriteLine("PASS native HTML widget renders and receives live audio event");}
 else if(painted&&p.Alpha<8){if(Program.Option("--widget-output") is{} output)File.WriteAllBytes(output,bytes);Console.WriteLine("PASS native HTML widget preserves transparency");timer?.Stop();browser.Dispose();life.Shutdown(0);return;}}if((DateTime.UtcNow-start).TotalSeconds>30)throw new Exception("HTML widget did not render the audio-reactive fixture.");}catch(Exception e){Console.Error.WriteLine(e);timer?.Stop();browser.Dispose();life.Shutdown(1);}finally{busy=false;}});timer.Start();
 }catch(Exception e){Console.Error.WriteLine(e);browser?.Dispose();life.Shutdown(1);}}
}
