#if WINDOWS
using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;

// The WinUI window has its own dispatcher. Only this renderer owns and saves settings.
internal sealed partial class WindowsSettingsHost(Func<JsonObject,JsonNode> handle):IDisposable {
 readonly string pipe="WirePulse-Settings-"+Guid.NewGuid().ToString("N");readonly CancellationTokenSource stop=new();Process? process;Task? serverTask;
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr hwnd,int command);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd);
 public bool Show(){
  string exe=Path.Combine(AppContext.BaseDirectory,"WindowsSettings","WirePulse.Settings.exe");if(!File.Exists(exe))return false;
  if(process is{HasExited:false}){process.Refresh();ShowWindow(process.MainWindowHandle,9);SetForegroundWindow(process.MainWindowHandle);return true;}
  serverTask??=Serve();var start=new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe)!};start.ArgumentList.Add("--pipe");start.ArgumentList.Add(pipe);if(Program.Option("--winui-test-output") is string output){start.ArgumentList.Add("--smoke-output");start.ArgumentList.Add(Path.GetFullPath(output));}process=Process.Start(start);return process!=null;
 }
 async Task Serve(){while(!stop.IsCancellationRequested){try{
  await using var server=new NamedPipeServerStream(pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
  await server.WaitForConnectionAsync(stop.Token);using var reader=new StreamReader(server,Encoding.UTF8,leaveOpen:true);using var writer=new StreamWriter(server,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};
  string? line=await reader.ReadLineAsync(stop.Token);if(line==null||line.Length>65536)continue;
  var request=JsonNode.Parse(line)?.AsObject()??throw new InvalidDataException("Solicitud inválida");JsonNode reply;
  try{if(request["op"]?.GetValue<string>()=="layout-export"){var get=new JsonObject{["op"]="layout-snapshot",["profile"]=request["profile"]?.DeepClone()};var snapshot=await Dispatcher.UIThread.InvokeAsync(()=>handle(get));var package=snapshot.Deserialize<LayoutPackage>(LayoutPackage.Json)!;await Task.Run(()=>LayoutPackage.Export(request["value"]!.GetValue<string>(),package));request["op"]="get";}if(request["op"]?.GetValue<string>()=="layout-import"){var package=await Task.Run(()=>LayoutPackage.Import(request["value"]!.GetValue<string>()));request["layout"]=JsonSerializer.SerializeToNode(package,LayoutPackage.Json);request["op"]="layout-apply";}if(request["op"]?.GetValue<string>()=="import"){string asset=await AssetStore.ImportMediaAsync(request["value"]!.GetValue<string>());request["op"]="set";request["key"]="backgroundimage";request["value"]=asset;}if(request["op"]?.GetValue<string>()=="editor-add"&&request["path"] is JsonValue pathValue){request["asset"]=await Task.Run(()=>AssetStore.Import(pathValue.GetValue<string>(),request["kind"]?.GetValue<string>()=="widget"));}reply=await Dispatcher.UIThread.InvokeAsync(()=>handle(request));}catch(Exception ex){reply=new JsonObject{["error"]=ex.Message};}
  await writer.WriteLineAsync(reply.ToJsonString());
 }catch(OperationCanceledException){break;}catch(Exception ex){Trace.WriteLine(ex);}}}
 public void Dispose(){stop.Cancel();if(process is{HasExited:false})process.CloseMainWindow();if(editorProcess is{HasExited:false})editorProcess.CloseMainWindow();}
}

public sealed partial class App {
 WindowsSettingsHost? windowsSettings;bool windowsAudioPaused;
 WindowsSettingsHost WindowsHost(){
  if(windowsSettings==null){windowsSettings=new WindowsSettingsHost(WindowsSettingsRequest);if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)life.Exit+=(_,_)=>windowsSettings.Dispose();}
  return windowsSettings;
 }
 bool ShowWindowsSettings()=>WindowsHost().Show();
 void ShowWindowsEditor()=>WindowsHost().ShowEditor();
 JsonNode WindowsSettingsRequest(JsonObject request){
  if(request["op"]?.GetValue<string>().StartsWith("editor-",StringComparison.Ordinal)==true)return editor?.WindowsRequest(request)??new JsonObject{["open"]=false};
  string op=request["op"]?.GetValue<string>()??"get",profile=request["profile"]?.GetValue<string>()??"";
  var target=settings.IndependentDisplays&&available.TryGetValue(profile,out var selected)?selected:settings;
  if(request["global"]?.GetValue<bool>()==true)target=settings;
  Avalonia.Size wallSize()=>walls.FirstOrDefault(w=>ReferenceEquals(w.View.Configuration,target))?.View.Bounds.Size??walls.FirstOrDefault()?.View.Bounds.Size??new Avalonia.Size(1920,1080);
  switch(op){
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
   case "startup":new StartupRegistration().SetEnabled(request["value"]!.GetValue<bool>());settings.StartupAsked=true;settings.Save();break;
   case "pause":settings.Paused=request["value"]!.GetValue<bool>();foreach(var p in profiles.Values)p.Paused=settings.Paused;break;
   case "audio":if(!windowsAudioPaused){audio.Dispose();windowsAudioPaused=true;}else{audio=new AudioCapture(spectrum);audio.Start();windowsAudioPaused=false;}break;
   case "restart":Dispatcher.UIThread.Post(()=>{Program.RestartRequested=true;(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();});break;
   case "test-finish":if(Program.Option("--winui-test-output")==null)throw new InvalidOperationException("Solo disponible en pruebas");Dispatcher.UIThread.Post(()=>(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());break;
   case "get":break;
   default:throw new ArgumentException("Acción desconocida");
  }
  object Values(Settings s)=>new{values=s.Values,scene=s.Scene!=null,profile=s.RenderProfile,backgroundStyle=s.BackgroundStyle,clockFont=s.ClockFont,suspendMaximized=s.SuspendMaximized,suspendFullscreen=s.SuspendFullscreen};
  var spectrumFrame=spectrum.Snapshot();bool startup=false;try{startup=new StartupRegistration().Enabled;}catch{}
  return JsonSerializer.SerializeToNode(new{global=Values(settings),displays=available.ToDictionary(p=>p.Key,p=>Values(p.Value)),specs=settings.Specs.Select(s=>new{key=s.Key,title=s.Title,kind=s.Kind,group=s.Group,min=s.Min,max=s.Max,step=s.Step}),independent=settings.IndependentDisplays,mediaSource=settings.MediaSource,ciderUrl=settings.CiderUrl,ciderToken=settings.CiderToken,engine=settings.WindowsEngine,renderer=settings.WindowsRenderer,activeEngine=RenderEngines.Requested,activeRenderer=GraphicsBackend.Requested,startup,paused=settings.Paused,audioRunning=!windowsAudioPaused,audio=windowsAudioPaused?"Captura detenida":audio.Status,sampleRate=spectrumFrame.SampleRate,fft=spectrumFrame.FftSize,bands=spectrumFrame.Bands,age=spectrumFrame.Age,bpm=PlaybackTempo.Resolve(media.Current,spectrumFrame.Bpm),media=media.Status,performance=walls.Select(w=>new{name=w.Name,fps=w.Covered?0:w.View.Statistics.Fps,target=w.Target,drawMs=w.View.Statistics.DrawMs,backend=w.View.EngineStatus,suspended=w.SuspensionReason})})!;
 }
}
#endif
