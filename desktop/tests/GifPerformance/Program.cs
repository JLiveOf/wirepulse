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

// Invalid timestamps must not poison the animation clock or prevent later valid frames.
using var timing=new ImageFrames(fixture);using var timingBitmap=new SKBitmap(640,360);using var timingCanvas=new SKCanvas(timingBitmap);
void ExpectFrame(int frame){codec.GetPixels(legacy.Info,legacy.GetPixels(),new SKCodecOptions(frame));timingCanvas.Clear(SKColors.Transparent);timing.Draw(timingCanvas,new(0,0,640,360));for(int y=0;y<360;y++)for(int x=0;x<640;x++)if(legacy.GetPixel(x,y)!=timingBitmap.GetPixel(x,y))throw new Exception($"Invalid elapsed time corrupted GIF frame {frame}");}
foreach(var dt in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1d}){timing.Update(dt,false);ExpectFrame(0);}
timing.Update(.040001,false);ExpectFrame(1);timing.Update(double.MaxValue,true);ExpectFrame(1);timing.Update(double.MaxValue,false);timing.Update(.04,false);
Console.WriteLine("PASS nonfinite/negative timestamps, paused clock and finite clock overflow");

using var baseline=new SKBitmap(640,360);using var referenceCanvas=new SKCanvas(baseline);using(var reference=new ImageFrames(fixture)){reference.Update(1.731,false);reference.Draw(referenceCanvas,new(0,0,640,360));}
foreach(int fps in new[]{60,75,165,240}){using var animation=new ImageFrames(fixture);for(int i=0;i<fps;i++)animation.Update(1.731/fps,false);using var image=new SKBitmap(640,360);using var c=new SKCanvas(image);animation.Draw(c,new(0,0,640,360));if(!baseline.Bytes.AsSpan().SequenceEqual(image.Bytes))throw new Exception("GIF speed depends on refresh rate: "+fps);}
Console.WriteLine("PASS GIF wall-clock equivalence at 60/75/165/240 FPS");
