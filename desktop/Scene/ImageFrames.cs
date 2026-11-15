using SkiaSharp;
namespace WirePulse;

/// Incremental GIF/WebP decoding. The codec resolves disposal/dependency frames; only one bitmap and image are retained.
public sealed class ImageFrames:IDisposable {
 readonly SKCodec codec;readonly SKBitmap bitmap;SKImage? image;readonly SKCodecFrameInfo[] frames;readonly int[] ends;readonly object gate=new();int index=-1;double clock;bool disposed;
 public int Width=>bitmap.Width;public int Height=>bitmap.Height;public bool Animated=>frames.Length>1;
 public ImageFrames(string path){codec=SKCodec.Create(path)??throw new InvalidDataException("No se puede abrir esta imagen.");if(codec.Info.Width>8192||codec.Info.Height>8192||(long)codec.Info.Width*codec.Info.Height>32_000_000){codec.Dispose();throw new InvalidDataException("La imagen es demasiado grande para animarla.");}bitmap=new SKBitmap(new SKImageInfo(codec.Info.Width,codec.Info.Height,SKColorType.Bgra8888,SKAlphaType.Premul));frames=codec.FrameInfo;ends=new int[Math.Max(1,frames.Length)];int total=0;for(int i=0;i<ends.Length;i++){total+=frames.Length>1?Math.Clamp(frames[i].Duration,20,60000):1000;ends[i]=total;}Update(0,false);}
 public void Update(double dt,bool paused){lock(gate){if(disposed)return;if(!paused)clock+=Math.Clamp(dt,0,.2);double time=clock*1000%ends[^1];int next=Array.FindIndex(ends,n=>time<n);if(next<0)next=0;if(next==index)return;var result=codec.GetPixels(bitmap.Info,bitmap.GetPixels(),new SKCodecOptions(next));if(result is SKCodecResult.Success or SKCodecResult.IncompleteInput){bitmap.NotifyPixelsChanged();var replacement=SKImage.FromBitmap(bitmap);image?.Dispose();image=replacement;index=next;}}}
 public void Draw(SKCanvas canvas,SKRect bounds,bool cover=false){lock(gate){if(image!=null&&!disposed){if(cover){float scale=Math.Max(bounds.Width/Width,bounds.Height/Height),w=bounds.Width/scale,h=bounds.Height/scale;canvas.DrawImage(image,new SKRect((Width-w)/2,(Height-h)/2,(Width+w)/2,(Height+h)/2),bounds);}else canvas.DrawImage(image,bounds);}}}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;image?.Dispose();bitmap.Dispose();codec.Dispose();}}
}
