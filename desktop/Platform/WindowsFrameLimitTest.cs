#if WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;
public sealed partial class App {
 void RunWindowsFrameLimitTest(IClassicDesktopStyleApplicationLifetime life,string output){
  settings=new Settings("fps-test-"+Guid.NewGuid()).Preview();media=new(settings);screenProbe=new Window();
  foreach(var screen in screenProbe.Screens.All){
   var configuration=settings.Preview();configuration.Scene=SceneDocument.Migrate(configuration,new Size(480,270));configuration.Values["unlimitedfps"]=System.Text.Json.Nodes.JsonValue.Create(true);
   var view=new WallpaperView(configuration,spectrum,media);var window=new Window{Title="WirePulse · prueba de límite FPS",Width=480,Height=270,Position=screen.Bounds.Position,WindowStartupLocation=WindowStartupLocation.Manual,Content=view,ShowInTaskbar=false};
   var surface=new Surface(window,view,screen.DisplayName??"Pantalla",DisplayTiming.RefreshRate(screen.Bounds),screen.Bounds);walls.Add(surface);window.Show();Schedule(surface);
  }
  var watch=Stopwatch.StartNew();var baseline=new Dictionary<Surface,long>();double measuredStart=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
  timer.Tick+=(_,_)=>{if(watch.Elapsed.TotalSeconds<2)return;if(baseline.Count==0){foreach(var wall in walls)baseline[wall]=wall.View.Frames;measuredStart=watch.Elapsed.TotalSeconds;return;}if(watch.Elapsed.TotalSeconds-measuredStart<6)return;timer.Stop();double elapsed=watch.Elapsed.TotalSeconds-measuredStart;var results=walls.Select(w=>new{name=w.Name,hz=w.Hz,target=w.Target,frames=w.View.Frames-baseline[w],fps=(w.View.Frames-baseline[w])/elapsed,reported=w.View.Statistics.Fps,engine=w.View.EngineStatus}).ToArray();bool valid=results.All(r=>r.frames>10&&r.fps<=r.hz+.5&&r.reported<=r.hz+1);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{valid,elapsed,displays=results},new JsonSerializerOptions{WriteIndented=true}));quitting=true;foreach(var w in walls){w.Active=false;w.Window.Close();w.View.Dispose();}screenProbe.Close();life.Shutdown(valid?0:1);};timer.Start();
 }
}
#endif
