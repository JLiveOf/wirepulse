using SkiaSharp;
using System.Diagnostics;
namespace WirePulse;

/// One sequential decoder per animation, immutable reusable frames, bounded shared cache budget.
/// The UI never waits for animated decoding; it keeps the last complete frame when decoding is late.
public sealed class ImageFrames:IDisposable {
 const long PerImageBudget=64L*1024*1024,GlobalBudget=128L*1024*1024;
 static long cachedBytes;static readonly SemaphoreSlim workers=new(Math.Clamp(Environment.ProcessorCount/4,1,2));
 readonly SKCodec codec;readonly SKBitmap bitmap;readonly SKCodecFrameInfo[] frames;readonly long[] ends;readonly SKImage?[] cache;readonly object gate=new(),decodeGate=new();readonly bool asynchronous;
 SKImage? image;int front=-1,decoded=-1,wanted,working;double clock;bool disposed;long reserved;
 public int Width{get;}public int Height{get;}public bool Animated=>frames.Length>1;public long DecodedFrames{get;private set;}public long CacheHits{get;private set;}public double DecodeMilliseconds{get;private set;}public event Action? FrameReady;
 public ImageFrames(string path,bool asynchronous=false){this.asynchronous=asynchronous;codec=SKCodec.Create(path)??throw new InvalidDataException("No se puede abrir esta imagen.");Width=codec.Info.Width;Height=codec.Info.Height;if(Width>8192||Height>8192||(long)Width*Height>32_000_000){codec.Dispose();throw new InvalidDataException("La imagen es demasiado grande para animarla.");}bitmap=new SKBitmap(new SKImageInfo(Width,Height,SKColorType.Bgra8888,SKAlphaType.Premul));frames=codec.FrameInfo;ends=new long[Math.Max(1,frames.Length)];cache=new SKImage?[ends.Length];long total=0;for(int i=0;i<ends.Length;i++){total+=Animated?Math.Clamp(frames[i].Duration,20,60000):1000;ends[i]=total;}Update(0,false);}
 public void Update(double dt,bool paused){int next;lock(gate){if(disposed)return;if(!paused&&double.IsFinite(dt)&&dt>0)clock=(clock+(dt%(ends[^1]/1000d)))%(ends[^1]/1000d);double time=clock*1000%ends[^1];next=Array.BinarySearch(ends,(long)time+1);if(next<0)next=~next;wanted=next;if(next==front)return;}
  if(!asynchronous){Decode(next);return;}if(Interlocked.CompareExchange(ref working,1,0)==0)_=Task.Run(Work);
 }
 async Task Work(){await workers.WaitAsync().ConfigureAwait(false);try{while(true){int next;lock(gate){if(disposed||wanted==front)return;next=wanted;}Decode(next);lock(gate){if(disposed||next==wanted)return;}}}catch(Exception){/* Keep the last valid frame; malformed frames must not crash the compositor. */}finally{workers.Release();Interlocked.Exchange(ref working,0);}}
 bool Reserve(long bytes){if(reserved+bytes>PerImageBudget)return false;while(true){long current=Interlocked.Read(ref cachedBytes);if(current+bytes>GlobalBudget)return false;if(Interlocked.CompareExchange(ref cachedBytes,current+bytes,current)==current){reserved+=bytes;return true;}}}
 void Decode(int next){bool notify=false;lock(decodeGate){lock(gate){if(disposed)return;if(cache[next] is{} hit){CacheHits++;Install(hit,next);notify=true;}}
   if(!notify){long start=Stopwatch.GetTimestamp();int prior=Animated&&decoded>=0&&decoded<next&&decoded>=frames[next].RequiredFrame?decoded:-1;var result=codec.GetPixels(bitmap.Info,bitmap.GetPixels(),new SKCodecOptions(next,prior));if(result is not(SKCodecResult.Success or SKCodecResult.IncompleteInput))return;bitmap.NotifyPixelsChanged();var replacement=SKImage.FromBitmap(bitmap);decoded=next;DecodedFrames++;DecodeMilliseconds+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;lock(gate){if(disposed){replacement.Dispose();return;}if(Animated&&Reserve((long)Width*Height*4))cache[next]=replacement;Install(replacement,next);notify=true;}}
  }if(notify)FrameReady?.Invoke();
 }
 void Install(SKImage replacement,int frame){var old=image;bool retained=front>=0&&ReferenceEquals(cache[front],old);image=replacement;front=frame;if(old!=null&&!retained&&!ReferenceEquals(old,replacement))old.Dispose();}
 public void Draw(SKCanvas canvas,SKRect bounds,bool cover=false){lock(gate){if(image==null||disposed)return;if(cover){float scale=Math.Max(bounds.Width/Width,bounds.Height/Height),w=bounds.Width/scale,h=bounds.Height/scale;canvas.DrawImage(image,new SKRect((Width-w)/2,(Height-h)/2,(Width+w)/2,(Height+h)/2),bounds);}else canvas.DrawImage(image,bounds);}}
 public void Dispose(){lock(decodeGate)lock(gate){if(disposed)return;disposed=true;if(front>=0&&!ReferenceEquals(cache[front],image))image?.Dispose();foreach(var entry in cache)entry?.Dispose();image=null;bitmap.Dispose();codec.Dispose();Interlocked.Add(ref cachedBytes,-reserved);reserved=0;}}
}
