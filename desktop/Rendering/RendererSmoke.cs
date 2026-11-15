using Avalonia.Controls;
using Avalonia;
using System.Text.Json.Nodes;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;

/// A real native window, without changing preferences, audio devices, login items or Explorer.
internal static class RendererSmoke {
 public static void Run(IClassicDesktopStyleApplicationLifetime life){
  var settings=new Settings("graphics-smoke-"+Guid.NewGuid());var spectrum=new Spectrum();var media=new MediaService(settings);bool desktop=Program.Args.Contains("--desktop-fixture"),legacy=Program.Args.Contains("--legacy-class-style");WindowsDesktopFixture? fixture=null;
  if(desktop){fixture=new WindowsDesktopFixture(Program.Args.Contains("--raised-desktop"),Program.Args.Contains("--explorer-desktop"));settings.Values["backgroundcolor"]=JsonValue.Create("#183652");foreach(string key in new[]{"card","spectrum","clockshow","bgshow"})settings.Values[key]=JsonValue.Create(false);}
  string? background=Program.Option("--background-fixture");if(background!=null){settings.Values["backgroundimage"]=JsonValue.Create(Path.GetFullPath(background));foreach(string key in new[]{"sphere","card","spectrum","clockshow","bgshow"})settings.Values[key]=JsonValue.Create(false);}
  if(RenderEngines.Native){settings.Values["sphereX"]=JsonValue.Create(50d);settings.Values["sphereY"]=JsonValue.Create(50d);settings.Values["radius"]=JsonValue.Create(35d);}
  settings.Scene=SceneDocument.Migrate(settings,new Size(640,360));
  var view=new WallpaperView(settings,spectrum,media);var window=new Window{Title="WirePulse renderer test",Width=640,Height=360,Content=view,WindowDecorations=WindowDecorations.None,CanResize=false};var watch=Stopwatch.StartNew();if(fixture!=null)window.Opened+=(_,_)=>view.UsePresenter(fixture.Attach(window.TryGetPlatformHandle()!.Handle,legacy));
  var pcm=Enumerable.Range(0,1600).Select(i=>(float)(.3*Math.Sin(2*Math.PI*110*i/48000))).ToArray();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};
  timer.Tick+=(_,_)=>{spectrum.Push(pcm,pcm.Length,1,48000);view.Advance(watch.Elapsed.TotalSeconds);if(watch.Elapsed.TotalSeconds<5)return;timer.Stop();var output=Program.Option("--probe-output");var pixels=fixture?.Check(output==null?null:Path.ChangeExtension(output,"png"));bool visible=pixels==null||(background==null?pixels.Value.Matching>=7:pixels.Value.MediaPixels>300);bool nativeValid=!RenderEngines.Native||(view.EngineStatus.Contains("textura compartida GPU")&&pixels?.Colored>300);bool valid=visible&&nativeValid&&(background==null||!BackdropMedia.IsVideo(background)||view.VideoFrames>5)&&view.Frames>5&&GraphicsBackend.Observed&&GraphicsBackend.FallbackOrder(GraphicsBackend.Requested).Contains(GraphicsBackend.PlatformBackend);if(output!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{engineRequested=RenderEngines.Requested,engine=view.EngineStatus,backgroundStatus=view.MediaBackdropStatus,videoFrames=view.VideoFrames,coloredPixels=pixels?.Colored,requested=GraphicsBackend.Requested,active=GraphicsBackend.PlatformBackend,description=view.Statistics.Backend,frames=view.Frames,desktop,legacy,visible,matching=pixels?.Matching,samples=pixels?.Samples,presentation=view.Presenter?.Info??DesktopHost.PresentationInfo(window.TryGetPlatformHandle()!.Handle),valid},new JsonSerializerOptions{WriteIndented=true}));}window.Close();view.Dispose();life.Shutdown(valid?0:1);};
  window.Show();timer.Start();life.Exit+=(_,_)=>{timer.Stop();view.Dispose();fixture?.Dispose();};
 }
}
