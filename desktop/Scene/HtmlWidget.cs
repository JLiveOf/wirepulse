using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;

public sealed class HtmlWidget:IDisposable {
 readonly Spectrum spectrum;readonly IWidgetBrowser? browser;readonly DispatcherTimer timer;bool busy,disposed;Size size=new(400,240);DateTime last;
 public Bitmap? Image{get;private set;}public string Status{get;private set;}="Cargando widget…";public event Action? Changed;
 public HtmlWidget(string path,Spectrum spectrum){this.spectrum=spectrum;try{if(!File.Exists(path))throw new FileNotFoundException("No se encuentra el widget.");
#if WINDOWS
 browser=new WindowsWidgetBrowser(path);
#else
 if(OperatingSystem.IsLinux())browser=new GtkWidgetBrowser(path);
#endif
 if(browser==null)Status="Motor de widgets no disponible.";
 }catch(Exception e){Status=e.Message;}timer=new DispatcherTimer(TimeSpan.FromMilliseconds(100),DispatcherPriority.Background,async(_,_)=>await Capture());timer.Start();}
 public void Resize(Size value){size=new(Math.Clamp(value.Width,16,1920),Math.Clamp(value.Height,16,1080));}
 public void Tick(bool paused){if(browser!=null){var s=spectrum.Snapshot();var data=JsonSerializer.Serialize(new{bands=s.Bands,beat=paused?0:s.Beat,bpm=s.Bpm,playing=!paused&&s.Age<.5});browser.Audio(data);}}
 async Task Capture(){if(disposed||busy||browser==null)return;busy=true;try{var bytes=await browser.Snapshot((int)size.Width,(int)size.Height);if(!disposed&&bytes is{Length:>0}){using var stream=new MemoryStream(bytes);var next=new Bitmap(stream);var old=Image;Image=next;Status="Widget HTML";Changed?.Invoke();old?.Dispose();last=DateTime.UtcNow;}}catch(Exception e){Status=e.Message;Changed?.Invoke();}finally{busy=false;}}
 public void Dispose(){disposed=true;timer.Stop();browser?.Dispose();Image?.Dispose();}
}
public interface IWidgetBrowser:IDisposable{Task<byte[]?> Snapshot(int width,int height);void Audio(string json);}
