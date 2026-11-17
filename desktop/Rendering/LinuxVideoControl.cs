using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
namespace WirePulse;

/// One bounded raw frame at a time. FFmpeg decodes off-thread; pausing stops the decoder process.
internal sealed class LinuxVideoControl:Control,IDisposable {
 readonly string path;readonly CancellationTokenSource stop=new();readonly object gate=new();Process? process;SKBitmap? bitmap;bool paused,disposed;long frames;string status="Cargando vídeo · FFmpeg";
 public string Status=>status;public long DecodedFrames=>Interlocked.Read(ref frames);
 [DllImport("libc",SetLastError=true)]static extern int kill(int pid,int signal);
 public LinuxVideoControl(string path){this.path=path;_=Task.Run(Run);}
 async Task Run(){try{
  using var probe=new Process{StartInfo=new("ffprobe"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true}};foreach(var a in new[]{"-v","error","-select_streams","v:0","-show_entries","stream=width,height","-of","json",path})probe.StartInfo.ArgumentList.Add(a);probe.Start();var output=probe.StandardOutput.ReadToEndAsync(stop.Token);var errors=probe.StandardError.ReadToEndAsync(stop.Token);using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){timeout.CancelAfter(5000);try{await probe.WaitForExitAsync(timeout.Token);}catch{try{probe.Kill(true);}catch{}throw;}}using var json=JsonDocument.Parse(await output);await errors;var stream=json.RootElement.GetProperty("streams")[0];int w=stream.GetProperty("width").GetInt32(),h=stream.GetProperty("height").GetInt32();if(w<=0||h<=0)throw new InvalidDataException("Vídeo sin dimensiones");double scale=Math.Min(1,Math.Min(1920d/w,1080d/h));w=Math.Max(2,(int)(w*scale)/2*2);h=Math.Max(2,(int)(h*scale)/2*2);
  var p=new Process{StartInfo=new("ffmpeg"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true}};foreach(var a in new[]{"-v","error","-nostdin","-re","-stream_loop","-1","-i",path,"-an","-sn","-vf",$"scale={w}:{h},fps=30","-threads","2","-f","rawvideo","-pix_fmt","bgra","pipe:1"})p.StartInfo.ArgumentList.Add(a);
  lock(gate){if(disposed){p.Dispose();return;}process=p;p.Start();if(paused)kill(p.Id,19);bitmap=new SKBitmap(new SKImageInfo(w,h,SKColorType.Bgra8888,SKAlphaType.Opaque));}var stderr=p.StandardError.ReadToEndAsync();var bytes=new byte[checked(w*h*4)];status="Vídeo · FFmpeg · sin audio";
  while(!stop.IsCancellationRequested){await p.StandardOutput.BaseStream.ReadExactlyAsync(bytes,stop.Token);lock(gate){if(disposed)return;Marshal.Copy(bytes,0,bitmap!.GetPixels(),bytes.Length);bitmap.NotifyPixelsChanged();}Interlocked.Increment(ref frames);Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);}
 }catch(OperationCanceledException){}catch(Exception e){if(!stop.IsCancellationRequested)status="No se pudo reproducir el vídeo: "+e.Message;}finally{lock(gate){if(process is{} p){try{if(!p.HasExited){kill(p.Id,18);p.Kill(true);}}catch{}p.Dispose();process=null;}}}}
 public void Advance(double seconds,bool pause){lock(gate){if(disposed||paused==pause)return;paused=pause;if(process is{} p)try{if(!p.HasExited)kill(p.Id,pause?19:18);}catch{}}}
 public override void Render(DrawingContext c)=>c.Custom(new Operation(this,new Rect(Bounds.Size)));
 void Draw(SKCanvas canvas,Rect bounds){lock(gate){if(disposed||bitmap==null||DecodedFrames==0)return;float scale=Math.Max((float)bounds.Width/bitmap.Width,(float)bounds.Height/bitmap.Height),w=(float)bounds.Width/scale,h=(float)bounds.Height/scale;canvas.DrawBitmap(bitmap,new SKRect((bitmap.Width-w)/2,(bitmap.Height-h)/2,(bitmap.Width+w)/2,(bitmap.Height+h)/2),new SKRect(0,0,(float)bounds.Width,(float)bounds.Height));}}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;stop.Cancel();if(process is{} p)try{if(!p.HasExited){kill(p.Id,18);p.Kill(true);}}catch{}bitmap?.Dispose();bitmap=null;}}
 sealed class Operation(LinuxVideoControl owner,Rect bounds):ICustomDrawOperation{public Rect Bounds=>bounds;public bool HitTest(Point p)=>false;public bool Equals(ICustomDrawOperation? other)=>false;public void Dispose(){}public void Render(ImmediateDrawingContext c){using var lease=c.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();if(lease!=null)owner.Draw(lease.SkCanvas,bounds);}}
}
