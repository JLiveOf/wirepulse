#if WINDOWS
using Avalonia.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;
public sealed partial class App {
 void TestNativeExperience(Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life){bool tested=false;int finishRequests=0,acknowledgements=0;var host=new WindowsSettingsHost(request=>{if(request["op"]?.GetValue<string>()=="tested")tested=true;if(request["op"]?.GetValue<string>()=="finish")finishRequests++;if(request["op"]?.GetValue<string>()=="ack")acknowledgements++;return JsonSerializer.SerializeToNode(new{finished=finishRequests==1,setup=true,news=true,direct=true,duration=0.01,version=UpdateService.InstalledVersion,notes=WhatsNewContent.Current.Features,startup=false,priority=false,automatic=true,channel="stable",splash=false},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})!;});if(!host.Show(true)){life.Shutdown(1);return;}var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};int ticks=0;timer.Tick+=(_,_)=>{if(!host.Alive||ticks++>100){timer.Stop();host.Dispose();bool valid=tested&&finishRequests==1&&acknowledgements==1;if(valid)Console.WriteLine("PASS WinUI splash, topmost native captions, close effect handoff, setup, release highlights and update actions");life.Shutdown(valid?0:1);}};timer.Start();}
}
#endif
