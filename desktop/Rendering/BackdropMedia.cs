using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
namespace WirePulse;

/// Media is independent of visualization geometry: selecting a draw engine never removes the background.
public sealed class BackdropMedia:Panel,IDisposable {
 readonly Settings settings;ImageFrames? image;ImageLayer? imageLayer;IDisposable? video;Action<double,bool>? advanceVideo;string source="";double previous;Func<string>? videoStatus;Func<long>? videoFrames;public long VideoFrames=>videoFrames?.Invoke()??0;string status="Sin archivo de fondo";public string Status=>videoStatus?.Invoke()??status;public bool Animated=>image?.Animated==true||video!=null;
 public BackdropMedia(Settings settings,bool enabled=true){this.settings=settings;ClipToBounds=true;if(enabled)Refresh();}
 public static bool IsVideo(string path)=>new[]{".mp4",".m4v",".mov",".wmv",".avi",".webm",".mkv"}.Contains(Path.GetExtension(path).ToLowerInvariant());
 public void Refresh(){string next=settings.Text("backgroundimage");if(next==source)return;source=next;Children.Clear();image?.Dispose();video?.Dispose();image=null;video=null;imageLayer=null;advanceVideo=null;videoStatus=null;videoFrames=null;previous=0;if(string.IsNullOrWhiteSpace(next)){status="Sin archivo de fondo";return;}try{if(IsVideo(next)){
#if WINDOWS
 var player=new NativeVideoControl(next);video=player;advanceVideo=player.Advance;videoStatus=()=>player.Status;videoFrames=()=>player.DecodedFrames;Children.Add(player);status="Vídeo · Media Foundation · sin audio";
#else
 status="La reproducción de vídeo de fondo está disponible en Windows.";
#endif
 }else{image=new ImageFrames(next);imageLayer=new ImageLayer(image);Children.Add(imageLayer);status=image.Animated?"GIF animado":"Imagen";}}catch(Exception e){status="No se pudo cargar el fondo: "+e.Message;}InvalidateVisual();}
 public void Advance(double seconds,bool paused){Refresh();double dt=previous==0?0:Math.Clamp(seconds-previous,0,.2);previous=seconds;image?.Update(dt,paused);if(image?.Animated==true)imageLayer?.InvalidateVisual();advanceVideo?.Invoke(seconds,paused);}
 public void Dispose(){image?.Dispose();video?.Dispose();image=null;video=null;}
 sealed class ImageLayer(ImageFrames image):Control{public override void Render(DrawingContext c){c.Custom(new DrawImage(image,new Rect(Bounds.Size)));}}
 sealed class DrawImage(ImageFrames image,Rect bounds):ICustomDrawOperation{public Rect Bounds=>bounds;public bool HitTest(Point p)=>false;public bool Equals(ICustomDrawOperation? o)=>false;public void Dispose(){}public void Render(ImmediateDrawingContext c){using var lease=c.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();if(lease!=null)image.Draw(lease.SkCanvas,new(0,0,(float)bounds.Width,(float)bounds.Height),cover:true);}}
}
