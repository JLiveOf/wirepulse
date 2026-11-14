using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;

/// A real native window, without changing preferences, audio devices, login items or Explorer.
internal static class RendererSmoke {
 public static void Run(IClassicDesktopStyleApplicationLifetime life){
  var settings=new Settings("graphics-smoke-"+Guid.NewGuid());var spectrum=new Spectrum();var media=new MediaService(settings);var view=new WallpaperView(settings,spectrum,media);var window=new Window{Title="WirePulse renderer test",Width=640,Height=360,Content=view};var watch=Stopwatch.StartNew();
  var pcm=Enumerable.Range(0,1600).Select(i=>(float)(.3*Math.Sin(2*Math.PI*110*i/48000))).ToArray();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};
  timer.Tick+=(_,_)=>{spectrum.Push(pcm,pcm.Length,1,48000);view.Advance(watch.Elapsed.TotalSeconds);if(watch.Elapsed.TotalSeconds<5)return;timer.Stop();bool valid=view.Frames>5&&GraphicsBackend.Observed&&GraphicsBackend.FallbackOrder(GraphicsBackend.Requested).Contains(GraphicsBackend.PlatformBackend);if(Program.Option("--probe-output") is{} output){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{requested=GraphicsBackend.Requested,active=GraphicsBackend.PlatformBackend,description=view.Statistics.Backend,frames=view.Frames,valid},new JsonSerializerOptions{WriteIndented=true}));}window.Close();view.Dispose();life.Shutdown(valid?0:1);};
  window.Show();timer.Start();life.Exit+=(_,_)=>{timer.Stop();view.Dispose();};
 }
}
