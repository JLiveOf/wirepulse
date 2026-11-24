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
internal sealed partial class WindowsSettingsHost(Func<JsonObject,JsonNode> handle):INativeUiHost {
 readonly string pipe="WirePulse-Settings-"+Guid.NewGuid().ToString("N");readonly CancellationTokenSource stop=new();Process? process;Task? serverTask;
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr hwnd,int command);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd);
 public bool Alive=>process is{HasExited:false};
 public bool Show(bool experience=false){
  string exe=Path.Combine(AppContext.BaseDirectory,"WindowsSettings","WirePulse.Settings.exe");if(!File.Exists(exe))return false;
  if(process is{HasExited:false}){process.Refresh();ShowWindow(process.MainWindowHandle,9);SetForegroundWindow(process.MainWindowHandle);return true;}
  serverTask??=Serve();var start=new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe)!};if(experience)start.ArgumentList.Add("--experience");if(Program.Args.Contains("--experience-self-test"))start.ArgumentList.Add("--experience-test");start.ArgumentList.Add("--pipe");start.ArgumentList.Add(pipe);if(Program.Option("--winui-test-output") is string output){start.ArgumentList.Add("--smoke-output");start.ArgumentList.Add(Path.GetFullPath(output));}process=Process.Start(start);return process!=null;
 }
 async Task Serve(){while(!stop.IsCancellationRequested){try{
  await using var server=new NamedPipeServerStream(pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
  await server.WaitForConnectionAsync(stop.Token);using var reader=new StreamReader(server,Encoding.UTF8,leaveOpen:true);using var writer=new StreamWriter(server,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};
  string? line=await reader.ReadLineAsync(stop.Token);if(line==null||line.Length>65536)continue;
  var request=JsonNode.Parse(line)?.AsObject()??throw new InvalidDataException("Solicitud inválida");JsonNode reply;
  try{if(request["op"]?.GetValue<string>()=="layout-export"){var get=new JsonObject{["op"]="layout-snapshot",["profile"]=request["profile"]?.DeepClone()};var snapshot=await Dispatcher.UIThread.InvokeAsync(()=>handle(get));var package=snapshot.Deserialize<LayoutPackage>(LayoutPackage.Json)!;await Task.Run(()=>LayoutPackage.Export(request["value"]!.GetValue<string>(),package));request["op"]="get";}if(request["op"]?.GetValue<string>()=="layout-import"){var package=await Task.Run(()=>LayoutPackage.Import(request["value"]!.GetValue<string>()));request["layout"]=JsonSerializer.SerializeToNode(package,LayoutPackage.Json);request["op"]="layout-apply";}if(request["op"]?.GetValue<string>()=="import"){string asset=await AssetStore.ImportMediaAsync(request["value"]!.GetValue<string>());request["op"]="set";request["key"]="backgroundimage";request["value"]=asset;}if(request["op"]?.GetValue<string>()=="editor-add"&&request["path"] is JsonValue pathValue){request["asset"]=await Task.Run(()=>AssetStore.Import(pathValue.GetValue<string>(),request["kind"]?.GetValue<string>()=="widget"));}reply=await Dispatcher.UIThread.InvokeAsync(()=>handle(request));}catch(Exception ex){reply=new JsonObject{["error"]=ex.Message};}
  await writer.WriteLineAsync(reply.ToJsonString());
 }catch(OperationCanceledException){break;}catch(Exception ex){Trace.WriteLine(ex);}}}
 public void Terminate(){Dispose();try{if(process is{HasExited:false})process.Kill();}catch(InvalidOperationException){}}
 public void Dispose(){stop.Cancel();if(process is{HasExited:false})process.CloseMainWindow();if(editorProcess is{HasExited:false})editorProcess.CloseMainWindow();}
}

public sealed partial class App {
 WindowsSettingsHost? windowsSettings;
 WindowsSettingsHost WindowsHost(){
  if(windowsSettings==null){windowsSettings=new WindowsSettingsHost(WindowsSettingsRequest);if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)life.Exit+=(_,_)=>windowsSettings.Dispose();}
  return windowsSettings;
 }
 bool ShowWindowsSettings()=>WindowsHost().Show();
 void ShowWindowsEditor()=>WindowsHost().ShowEditor();
}
#endif
