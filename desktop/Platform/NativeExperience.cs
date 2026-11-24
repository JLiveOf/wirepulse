using Avalonia.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;
public sealed partial class App {
 INativeUiHost CreateExperienceHost(){
#if WINDOWS
 return new WindowsSettingsHost(ExperienceRequest);
#else
 return new LinuxSettingsHost(ExperienceRequest);
#endif
 }
 INativeUiHost? experience;SetupMusic? experienceMusic;WelcomeSound? experienceSound;UpdateRelease? experienceRelease;bool experienceSetup,experienceNews;string experienceStatus="";double experienceProgress;bool experienceInstalling,experienceFinished,experienceHandedOff;
 bool ShowNativeExperience(bool setup,bool news,bool direct=false){if(!OperatingSystem.IsWindows()&&(!OperatingSystem.IsLinux()||!LinuxSettingsHost.Available))return false;if(experience!=null){return true;}experienceSetup=setup;experienceNews=news;experienceRelease=null;experienceStatus="";experienceFinished=false;experienceHandedOff=false;nativeStarted=false;experience=CreateExperienceHost();if(!experience.Show(true)){experience.Dispose();experience=null;return false;}welcomePending=true;experienceSound=new();nativeDirect=direct;return true;}
 bool nativeDirect,nativeStarted;
 bool ShowNativeUpdate(UpdateRelease release){if(!ShowNativeExperience(false,false,true))return false;experienceRelease=release;return true;}
 void MaintainNativeExperience(){if(experience!=null&&!experience.Alive){experience.Dispose();experience=null;welcomePending=false;experienceSound?.Dispose();experienceMusic?.Dispose();experienceSound=null;experienceMusic=null;experienceRelease=null;if(experienceFinished)_=UpdateService.Current.CheckAsync();}}
 JsonNode ExperienceRequest(JsonObject request){string op=request["op"]?.GetValue<string>()??"get";
  if(op=="start"&&!nativeStarted&&!nativeDirect){nativeStarted=true;experienceSound?.Play();}
  if(op=="expanded"){experienceSound?.Dispose();experienceMusic??=new();experienceMusic.Start();}
  if(op=="mute"&&experienceMusic!=null)experienceMusic.Muted=!experienceMusic.Muted;
  if(op=="save"){var registration=new StartupRegistration();bool enabled=request["startup"]?.GetValue<bool>()==true;if(enabled&&request["priority"]?.GetValue<bool>()==true)registration.SetPriority(true);else registration.SetEnabled(enabled);UpdateService.Current.Configure(channel:request["channel"]?.GetValue<string>(),automatic:request["automatic"]?.GetValue<bool>());settings.StartupAsked=true;settings.Save();welcomeState.ShowOnStartup=request["splash"]?.GetValue<bool>()==true;welcomeState.Complete(UpdateService.InstalledVersion);experienceSetup=false;}
  if(op=="finish"){_=FinishNativeExperience();}
  if(op=="ack")welcomeState.CompleteNews(UpdateService.NewsIdentity);
  if(op=="skip"&&experienceRelease!=null)UpdateService.Current.Configure(skipped:experienceRelease.Identity);
  if(op=="never")UpdateService.Current.Configure(automatic:false);
  if(op=="install"&&experienceRelease!=null&&!experienceInstalling){experienceInstalling=true;_=InstallNativeUpdate(experienceRelease);}
  var start=new StartupRegistration();return JsonSerializer.SerializeToNode(new{language=L10n.Language,setup=experienceSetup,news=experienceNews,direct=nativeDirect,duration=WelcomeWindow.IntroDuration,version=UpdateService.InstalledVersion,notes=WhatsNewContent.Current.Features,startup=start.Enabled,priority=start.PriorityEnabled,automatic=UpdateService.Current.Preferences.Automatic,channel=UpdateService.Current.Preferences.Channel,splash=welcomeState.ShowOnStartup,muted=experienceMusic?.Muted??false,update=experienceRelease?.DisplayTag,status=experienceStatus,progress=experienceProgress,installing=experienceInstalling,handedOff=experienceHandedOff,finished=experienceFinished},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})!;
 }
 async Task FinishNativeExperience(){experienceMusic?.Dispose();experienceSound?.Dispose();using var effect=new WelcomeSound();effect.Play("Shutdown.wav");await Task.Delay(600);experienceFinished=true;}
 async Task InstallNativeUpdate(UpdateRelease release){try{experienceStatus=L10n.Text("Descargando actualización…");var package=await UpdateService.Current.DownloadAsync(release,new Progress<double>(p=>experienceProgress=p),CancellationToken.None);experienceStatus=L10n.Text("Instalando actualización…");if(await UpdateInstaller.StartAsync(package,release.Tag)){experienceInstalling=false;await Task.Delay(500);experience?.Terminate();(ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();}else{experienceHandedOff=true;experienceStatus=L10n.Text("Completa la instalación en el gestor de paquetes y reinicia WirePulse.");}}catch(Exception error){experienceStatus=L10n.Text("No se pudo actualizar: ")+error.Message;}finally{experienceInstalling=false;}}
}
