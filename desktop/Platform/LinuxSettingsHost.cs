using Avalonia.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;
internal interface INativeUiHost:IDisposable {bool Alive{get;}bool Show(bool experience=false);void Terminate();}
internal sealed class LinuxSettingsHost(Func<JsonObject,JsonNode> handle):INativeUiHost {
 static readonly Lazy<bool> gtk=new(()=>{try{if(!File.Exists("/usr/bin/python3"))return false;var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true};start.ArgumentList.Add("-c");start.ArgumentList.Add("import gi,cairo;gi.require_foreign('cairo');gi.require_version('Gtk','3.0');from gi.repository import Gtk");using var process=Process.Start(start)!;if(!process.WaitForExit(2000)){process.Kill(true);return false;}return process.ExitCode==0;}catch{return false;}});
 public static bool Available=>File.Exists(Path.Combine(AppContext.BaseDirectory,"LinuxUI","native.py"))&&gtk.Value;
 readonly string folder=Path.Combine(Path.GetTempPath(),"wirepulse-ui-"+Guid.NewGuid().ToString("N"));readonly CancellationTokenSource stop=new();Socket? listener;Process? process,editor;Task? server;
 public bool Alive=>process is{HasExited:false};
 public bool Show(bool experience=false)=>Start(ref process,experience?"experience":"settings");
 public bool ShowEditor()=>Start(ref editor,"editor");
 bool Start(ref Process? child,string mode){if(!Available)return false;if(child is{HasExited:false}){_ = SendFocus(mode);return true;}child?.Dispose();if(listener==null){Directory.CreateDirectory(folder);File.SetUnixFileMode(folder,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);listener=new(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);listener.Bind(new UnixDomainSocketEndPoint(Path.Combine(folder,"rpc")));listener.Listen(8);server=Serve();}
  var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,WorkingDirectory=AppContext.BaseDirectory};foreach(string arg in new[]{Path.Combine(AppContext.BaseDirectory,"LinuxUI","native.py"),"--socket",Path.Combine(folder,"rpc"),"--mode",mode})start.ArgumentList.Add(arg);if(Program.Option("--gtk-ui-test-output") is string report){start.ArgumentList.Add("--test-output");start.ArgumentList.Add(Path.GetFullPath(report));}child=Process.Start(start);return child!=null;
 }
 Task SendFocus(string mode){try{File.WriteAllText(Path.Combine(folder,"focus-"+mode),Guid.NewGuid().ToString());}catch(IOException){}return Task.CompletedTask;}
 async Task Serve(){while(!stop.IsCancellationRequested){try{using var client=await listener!.AcceptAsync(stop.Token);await using var stream=new NetworkStream(client);using var reader=new StreamReader(stream,Encoding.UTF8,leaveOpen:true);using var writer=new StreamWriter(stream,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};string? text=await reader.ReadLineAsync(stop.Token);if(text==null||text.Length>2_000_000)continue;JsonNode reply;
   try{var request=JsonNode.Parse(text)!.AsObject();string op=request["op"]?.GetValue<string>()??"get";
    if(op=="layout-export"){var snapshot=await Dispatcher.UIThread.InvokeAsync(()=>handle(new(){["op"]="layout-snapshot",["profile"]=request["profile"]?.DeepClone()}));await Task.Run(()=>LayoutPackage.Export(request["value"]!.GetValue<string>(),snapshot.Deserialize<LayoutPackage>(LayoutPackage.Json)!));request["op"]="get";}
    if(op=="layout-import"){var package=await Task.Run(()=>LayoutPackage.Import(request["value"]!.GetValue<string>()));request["layout"]=JsonSerializer.SerializeToNode(package,LayoutPackage.Json);request["op"]="layout-apply";}
    if(op=="import"){request["value"]=await AssetStore.ImportMediaAsync(request["value"]!.GetValue<string>());request["key"]="backgroundimage";request["op"]="set";}
    if(op=="editor-add"&&request["path"] is JsonValue path){request["asset"]=await Task.Run(()=>AssetStore.Import(path.GetValue<string>(),request["kind"]?.GetValue<string>()=="widget"));}
    reply=await Dispatcher.UIThread.InvokeAsync(()=>handle(request));
   }catch(Exception e){reply=new JsonObject{["error"]=e.Message};}await writer.WriteLineAsync(reply.ToJsonString());
  }catch(OperationCanceledException){break;}catch(ObjectDisposedException){break;}catch(Exception e){Trace.WriteLine(e);}}}
 public void Terminate(){foreach(var child in new[]{process,editor})try{if(child is{HasExited:false})child.Kill(true);}catch(InvalidOperationException){}Dispose();}
 public void Dispose(){stop.Cancel();listener?.Dispose();foreach(var child in new[]{process,editor})try{if(child is{HasExited:false})child.Kill(true);}catch(InvalidOperationException){}try{Directory.Delete(folder,true);}catch(IOException){}}
}
public sealed partial class App {
 LinuxSettingsHost? linuxSettings;
 LinuxSettingsHost LinuxHost(){if(linuxSettings==null){linuxSettings=new(WindowsSettingsRequest);if(ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life)life.Exit+=(_,_)=>linuxSettings.Dispose();}return linuxSettings;}
 bool ShowLinuxSettings(){if(welcomePending||welcome!=null)return false;return LinuxHost().Show();}
 void ShowLinuxEditor()=>LinuxHost().ShowEditor();
}
