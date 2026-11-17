#if WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;

// A bounded native video test; never changes wallpaper, audio or saved preferences.
internal static class WindowsVideoTest {
 public static void Run(IClassicDesktopStyleApplicationLifetime life){
  if(Program.Args.Contains("--stress-disposal")){RunLifecycle(life);return;}
  string path=Program.Option("--background-fixture")??throw new ArgumentException("Missing --background-fixture");
  var player=new NativeVideoControl(Path.GetFullPath(path));
  var window=new Window{Title="WirePulse · prueba de vídeo Windows",Width=960,Height=540,Content=player,ShowActivated=false};
  var watch=new Stopwatch();var startup=Stopwatch.StartNew();bool startupFailed=false;long requests=0,pauseStart=-1,pauseEnd=-1;double cpuStart=Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
  var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(4)};
  timer.Tick+=(_,_)=>{
   // Cold software adapters may initialize after the original seven-second sample window.
   // Start cadence/pause measurements only after a real frame; retain a bounded startup timeout.
   if(!watch.IsRunning){player.Advance(0,false);if(player.Ready&&player.DecodedFrames>0){watch.Start();cpuStart=Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;}else if(startup.Elapsed.TotalSeconds<30)return;else startupFailed=true;}
   double elapsed=startupFailed?7:watch.Elapsed.TotalSeconds;bool paused=elapsed>=3&&elapsed<4.5;
   player.Advance(elapsed,paused);requests++;
   // Leave time for any submitted frame to finish before checking the pause.
   if(elapsed>=3.5&&pauseStart<0)pauseStart=player.Statistics.Frames;
   if(elapsed>=4.4&&pauseEnd<0)pauseEnd=player.Statistics.Frames;
   if(elapsed<7)return;
   timer.Stop();long transfers=player.Statistics.Frames;
   bool valid=!startupFailed&&player.Ready&&player.DecodedFrames>15;
   if(Program.Args.Contains("--verify-video-cadence"))valid&=pauseStart==pauseEnd&&transfers<requests*.8;
   var report=new{valid,startupFailed,startupSeconds=startup.Elapsed.TotalSeconds-watch.Elapsed.TotalSeconds,requests,transfers,decodedFrames=player.DecodedFrames,pauseStart,pauseEnd,status=player.Status,cpuSeconds=Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds-cpuStart,elapsedSeconds=elapsed};
   string output=Program.Option("--probe-output")??"windows-video.json";Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
   window.Close();life.Shutdown(valid?0:1);
  };
  life.Exit+=(_,_)=>{timer.Stop();player.Dispose();};window.Show();timer.Start();
 }
 static void RunLifecycle(IClassicDesktopStyleApplicationLifetime life){
  string path=Path.GetFullPath(Program.Option("--background-fixture")??throw new ArgumentException("Missing video"));
  var player=new NativeVideoControl(path);var window=new Window{Title="WirePulse · prueba de recursos GPU",Width=800,Height=450,Content=player,ShowActivated=false};
  var watch=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(8)};int replaced=0,ready=0;double last=0;
  timer.Tick+=(_,_)=>{
   double now=watch.Elapsed.TotalSeconds;player.Advance(now,false);
   if(now-last<.4||(!player.Ready&&now-last<2))return;
   if(player.Ready)ready++;
   if(replaced<12){last=now;window.Content=null;player.Dispose();player=new NativeVideoControl(path);window.Content=player;replaced++;window.Width=replaced%2==0?800:960;return;}
   timer.Stop();bool valid=ready>0&&player.Ready;string output=Program.Option("--probe-output")??"video-lifecycle.json";Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{valid,replaced,ready,finalFrames=player.DecodedFrames},new JsonSerializerOptions{WriteIndented=true}));window.Close();life.Shutdown(valid?0:1);
  };
  life.Exit+=(_,_)=>{timer.Stop();player.Dispose();};window.Show();timer.Start();
 }
}
#endif
