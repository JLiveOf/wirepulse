using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;
public sealed partial class App {
 JsonNode WindowsSettingsRequest(JsonObject request){
  if(request["op"]?.GetValue<string>().StartsWith("editor-",StringComparison.Ordinal)==true)return editor?.WindowsRequest(request)??new JsonObject{["open"]=false};
  string op=request["op"]?.GetValue<string>()??"get",profile=request["profile"]?.GetValue<string>()??"";
  var target=settings.IndependentDisplays&&available.TryGetValue(profile,out var selected)?selected:settings;
  if(request["global"]?.GetValue<bool>()==true)target=settings;
  Avalonia.Size wallSize()=>walls.FirstOrDefault(w=>ReferenceEquals(w.View.Configuration,target))?.View.Bounds.Size??walls.FirstOrDefault()?.View.Bounds.Size??new Avalonia.Size(1920,1080);
  switch(op){
   case "gtk-test-welcome":if(Program.Option("--gtk-ui-test-output")==null)throw new InvalidOperationException("Solo para pruebas");Dispatcher.UIThread.Post(()=>ShowNativeExperience(true,true));break;
   case "gtk-test-finish":{var output=Program.Option("--gtk-ui-test-output")??throw new InvalidOperationException("Solo para pruebas");if(settings.Scene?.Items.Any(i=>i.Kind=="text")!=true||settings.Scene.Items.Any(i=>i.Kind=="image")!=true)throw new InvalidOperationException("El editor no guardó sus objetos");File.WriteAllText(output,request["value"]!.ToJsonString());DispatcherTimer.RunOnce(()=>(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(),TimeSpan.FromMilliseconds(500));break;}
   case "language":L10n.SetPreference(request["value"]!.GetValue<string>());break;
   case "show-news":Dispatcher.UIThread.Post(ShowReleaseNotes);break;
   case "update-channel":UpdateService.Current.Configure(channel:request["value"]!.GetValue<string>());break;
   case "show-splash":welcomeState.ShowOnStartup=request["value"]!.GetValue<bool>();break;
   case "update-automatic":UpdateService.Current.Configure(automatic:request["value"]!.GetValue<bool>());break;
   case "update-check":_=UpdateService.Current.CheckAsync(true);break;
   case "gradient":{string key=request["key"]!.GetValue<string>();if(!target.Specs.Any(s=>s.Key==key&&s.Kind=="color"))throw new ArgumentException("Propiedad desconocida");target.Gradients[key]=(request["value"]!.Deserialize<GradientSpec>(LayoutPackage.Json)??new()).Clean();target.Save();break;}
   case "set":{
    string key=request["key"]!.GetValue<string>();var spec=target.Specs.FirstOrDefault(s=>s.Key==key)??throw new ArgumentException("Control desconocido");var value=request["value"];
    if(spec.Kind=="slider"){double number=value!.GetValue<double>();if(!double.IsFinite(number))throw new ArgumentException("Valor inválido");target.Set(key,Math.Clamp(number,spec.Min,spec.Max));}
    else if(spec.Kind is "bool" or "toggle")target.Set(key,value!.GetValue<bool>());
    else{string text=value!.GetValue<string>();if(spec.Kind=="color"&&!Avalonia.Media.Color.TryParse(text,out _))throw new ArgumentException("Color inválido");target.Set(key,text);}break;
   }
   case "property":{
    string key=request["key"]!.GetValue<string>();
    string[] allowed=["IndependentDisplays","RenderProfile","SuspendMaximized","SuspendFullscreen","BackgroundStyle","ClockFont","MediaSource","CiderUrl","CiderToken","WindowsEngine","WindowsRenderer"];
    if(!allowed.Contains(key))throw new ArgumentException("Propiedad desconocida");var property=typeof(Settings).GetProperty(key)!;var value=request["value"]!.Deserialize(property.PropertyType);
    if(key=="CiderUrl"&&(!(value is string url)||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||!uri.IsLoopback||uri.Scheme!="http"))throw new ArgumentException("Cider requiere una dirección HTTP local.");
    if(key=="CiderUrl"&&settings.CiderUrl!=(string?)value)settings.CiderToken="";
    if(key=="WindowsEngine")value=RenderEngines.Normalize((string?)value);if(key=="WindowsRenderer")value=GraphicsBackend.Normalize((string?)value);
    property.SetValue(target,value);target.Save();break;
   }
   case "layout-snapshot":if(editor!=null)throw new InvalidOperationException("Guarda o cancela el editor antes de exportar.");return JsonSerializer.SerializeToNode(LayoutPackage.Create(target,wallSize()),LayoutPackage.Json)!;
   case "layout-apply":if(editor!=null)throw new InvalidOperationException("Guarda o cancela el editor antes de importar.");request["layout"]!.Deserialize<LayoutPackage>(LayoutPackage.Json)!.Apply(target);break;
   case "edit":ShowEditor(target,null);break;
   case "reset":target.Reset();break;
   case "startup-priority":new StartupRegistration().SetPriority(request["value"]!.GetValue<bool>());settings.StartupAsked=true;settings.Save();break;
   case "startup":new StartupRegistration().SetEnabled(request["value"]!.GetValue<bool>());settings.StartupAsked=true;settings.Save();break;
   case "pause":settings.Paused=request["value"]!.GetValue<bool>();foreach(var p in profiles.Values)p.Paused=settings.Paused;break;
   case "audio":if(!audioPaused){audio.Dispose();audioPaused=true;}else{audio=new AudioCapture(spectrum);audio.Start();audioPaused=false;}break;
   case "restart":Dispatcher.UIThread.Post(()=>{Program.RestartRequested=true;(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();});break;
   case "test-finish":if(Program.Option("--winui-test-output")==null)throw new InvalidOperationException("Solo disponible en pruebas");Dispatcher.UIThread.Post(()=>(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());break;
   case "get":break;
   default:throw new ArgumentException("Acción desconocida");
  }
  object Values(Settings s)=>new{gradients=s.Gradients,values=s.Values,scene=s.Scene!=null,profile=s.RenderProfile,backgroundStyle=s.BackgroundStyle,clockFont=s.ClockFont,suspendMaximized=s.SuspendMaximized,suspendFullscreen=s.SuspendFullscreen};
  var spectrumFrame=spectrum.Snapshot();bool startup=false,startupPriority=false;try{var registration=new StartupRegistration();startup=registration.Enabled;startupPriority=registration.PriorityEnabled;}catch{}
  return JsonSerializer.SerializeToNode(new{language=L10n.Language,languagePreference=L10n.Preference,showSplash=welcomeState.ShowOnStartup,updates=new{channel=UpdateService.Current.Preferences.Channel,automatic=UpdateService.Current.Preferences.Automatic,status=L10n.Text(UpdateService.Current.Status),installed=UpdateService.InstalledVersion},global=Values(settings),displays=available.ToDictionary(p=>p.Key,p=>Values(p.Value)),specs=settings.Specs.Select(s=>new{key=s.Key,title=s.Title,kind=s.Kind,group=s.Group,min=s.Min,max=s.Max,step=s.Step}),independent=settings.IndependentDisplays,mediaSource=settings.MediaSource,ciderUrl=settings.CiderUrl,ciderToken=settings.CiderToken,engine=settings.WindowsEngine,renderer=settings.WindowsRenderer,activeEngine=RenderEngines.Requested,activeRenderer=GraphicsBackend.Requested,startup,startupPriority,paused=settings.Paused,audioRunning=!audioPaused,audio=L10n.Text(audioPaused?"Captura detenida":audio.Status),sampleRate=spectrumFrame.SampleRate,fft=spectrumFrame.FftSize,bands=spectrumFrame.Bands,age=spectrumFrame.Age,bpm=PlaybackTempo.Resolve(media.Current,spectrumFrame.Bpm),media=L10n.Text(media.Status),performance=walls.Select(w=>new{name=w.Name,fps=w.Covered?0:w.View.Statistics.Fps,target=w.Target,drawMs=w.View.Statistics.DrawMs,backend=w.View.EngineStatus,suspended=w.SuspensionReason})})!;
 }
}
