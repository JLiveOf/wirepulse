#if WINDOWS
using Avalonia.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;
public sealed partial class App {
 void TestNativeExperience(Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life){bool tested=false;var host=new WindowsSettingsHost(request=>{if(request["op"]?.GetValue<string>()=="tested")tested=true;return JsonSerializer.SerializeToNode(new{setup=true,news=true,direct=true,duration=0.01,version=UpdateService.InstalledVersion,notes=WhatsNewContent.Current.Features,startup=false,priority=false,automatic=true,channel="stable",splash=false},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})!;});if(!host.Show(true)){life.Shutdown(1);return;}var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};int ticks=0;timer.Tick+=(_,_)=>{if(tested||!host.Alive||ticks++>100){timer.Stop();host.Dispose();if(tested)Console.WriteLine("PASS WinUI splash, setup, release highlights and update actions");life.Shutdown(tested?0:1);}};timer.Start();}
 WindowsSettingsHost? experience;SetupMusic? experienceMusic;WelcomeSound? experienceSound;UpdateRelease? experienceRelease;bool experienceSetup,experienceNews;string experienceStatus="";double experienceProgress;bool experienceInstalling,experienceFinished;
 bool ShowNativeExperience(bool setup,bool news,bool direct=false){if(experience!=null){return true;}experienceSetup=setup;experienceNews=news;experienceRelease=null;experienceStatus="";experienceFinished=false;nativeStarted=false;experience=new WindowsSettingsHost(ExperienceRequest);if(!experience.Show(true)){experience.Dispose();experience=null;return false;}welcomePending=true;experienceSound=new();nativeDirect=direct;return true;}
 bool nativeDirect,nativeStarted;
 bool ShowNativeUpdate(UpdateRelease release){if(!ShowNativeExperience(false,false,true))return false;experienceRelease=release;return true;}
 void MaintainNativeExperience(){if(experience!=null&&!experience.Alive){experience.Dispose();experience=null;welcomePending=false;experienceSound?.Dispose();experienceMusic?.Dispose();experienceSound=null;experienceMusic=null;experienceRelease=null;}}
 JsonNode ExperienceRequest(JsonObject request){string op=request["op"]?.GetValue<string>()??"get";
  if(op=="start"&&!nativeStarted&&!nativeDirect){nativeStarted=true;experienceSound?.Play();}
  if(op=="expanded"){experienceSound?.Dispose();experienceMusic??=new();experienceMusic.Start();}
  if(op=="mute"&&experienceMusic!=null)experienceMusic.Muted=!experienceMusic.Muted;
  if(op=="save"){var registration=new StartupRegistration();bool enabled=request["startup"]?.GetValue<bool>()==true;if(enabled&&request["priority"]?.GetValue<bool>()==true)registration.SetPriority(true);else registration.SetEnabled(enabled);UpdateService.Current.Configure(channel:request["channel"]?.GetValue<string>(),automatic:request["automatic"]?.GetValue<bool>());settings.StartupAsked=true;settings.Save();welcomeState.ShowOnStartup=request["splash"]?.GetValue<bool>()==true;welcomeState.Complete(UpdateService.InstalledVersion);experienceSetup=false;}
  if(op=="finish"){_=FinishNativeExperience();}
  if(op=="ack")welcomeState.CompleteNews(UpdateService.InstalledVersion);
  if(op=="skip"&&experienceRelease!=null)UpdateService.Current.Configure(skipped:experienceRelease.Tag);
  if(op=="never")UpdateService.Current.Configure(automatic:false);
  if(op=="install"&&experienceRelease!=null&&!experienceInstalling){experienceInstalling=true;_=InstallNativeUpdate(experienceRelease);}
  var start=new StartupRegistration();return JsonSerializer.SerializeToNode(new{setup=experienceSetup,news=experienceNews,direct=nativeDirect,duration=WelcomeWindow.IntroDuration,version=UpdateService.InstalledVersion,notes=WhatsNewContent.Current.Features,startup=start.Enabled,priority=start.PriorityEnabled,automatic=UpdateService.Current.Preferences.Automatic,channel=UpdateService.Current.Preferences.Channel,splash=welcomeState.ShowOnStartup,muted=experienceMusic?.Muted??false,update=experienceRelease?.Tag,status=experienceStatus,progress=experienceProgress,installing=experienceInstalling,finished=experienceFinished},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})!;
 }
 async Task FinishNativeExperience(){experienceMusic?.Dispose();experienceSound?.Dispose();using var effect=new WelcomeSound();effect.Play("Shutdown.wav");await Task.Delay(600);experienceFinished=true;}
 async Task InstallNativeUpdate(UpdateRelease release){try{experienceStatus=L10n.Text("Descargando actualización…");var package=await UpdateService.Current.DownloadAsync(release,new Progress<double>(p=>experienceProgress=p),CancellationToken.None);experienceStatus=L10n.Text("Instalando actualización…");if(await UpdateInstaller.StartAsync(package,release.Tag)){experienceInstalling=false;await Task.Delay(500);experience?.Dispose();(ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();}}catch(Exception error){experienceStatus=L10n.Text("No se pudo actualizar: ")+error.Message;}finally{experienceInstalling=false;}}
}
#endif
