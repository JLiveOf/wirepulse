using SkiaSharp;
using WirePulse;
using System.Diagnostics;
string fixture=Path.Combine(AppContext.BaseDirectory,"dependent.gif");using var codec=SKCodec.Create(fixture);using var legacy=new SKBitmap(codec.Info);using var actual=new SKBitmap(codec.Info);using var canvas=new SKCanvas(actual);using var frames=new ImageFrames(fixture);int count=codec.FrameCount;double oldMs=0;long start;
for(int loop=0;loop<3;loop++)for(int i=0;i<count;i++){
 start=Stopwatch.GetTimestamp();var result=codec.GetPixels(legacy.Info,legacy.GetPixels(),new SKCodecOptions(i));oldMs+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;if(result!=SKCodecResult.Success)throw new Exception("Fixture decode failed");
 if(loop!=0||i!=0)frames.Update(.040001,false);canvas.Clear(SKColors.Transparent);frames.Draw(canvas,new SKRect(0,0,640,360));
 // Compare complete output, including delta frames across loops, against the original decoder.
 for(int y=0;y<360;y++)for(int x=0;x<640;x++){var a=legacy.GetPixel(x,y);var b=actual.GetPixel(x,y);if(a!=b)throw new Exception($"GIF differs loop={loop} frame={i} at {x},{y}: {a} != {b}");}
}
if(frames.CacheHits==0)throw new Exception("Animation did not reuse cached frames");Console.WriteLine($"PASS {count*3} full GIF frame comparisons; old decode {oldMs:F1} ms; new decode {frames.DecodeMilliseconds:F1} ms; decoded={frames.DecodedFrames}; cache hits={frames.CacheHits}");
using var asyncFrames=new ImageFrames(fixture,asynchronous:true);var watch=Stopwatch.StartNew();for(int i=0;i<200;i++)asyncFrames.Update(.04,false);watch.Stop();if(watch.Elapsed.TotalMilliseconds>100)throw new Exception("UI update blocked on animated decoding");await Task.Delay(300);using var output=new SKBitmap(640,360);using var draw=new SKCanvas(output);asyncFrames.Draw(draw,new(0,0,640,360));if(asyncFrames.DecodedFrames==0)throw new Exception("Background decoder did not complete");Console.WriteLine($"PASS asynchronous UI submission: {watch.Elapsed.TotalMilliseconds:F2} ms for 200 updates; pause, resume and disposal bounded");
