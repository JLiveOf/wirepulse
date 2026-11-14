using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using System.Diagnostics;
using System.Text.Json;
using WirePulse;
AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).UseSkia().WithInterFont().SetupWithoutStarting();
// Exercise the counted C ABI on every native platform, even on CI without a physical GPU.
if(!SkiaLines.Available)throw new Exception("Native Skia line batching export could not be loaded");
using(var surface=SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(32,32))){surface.Canvas.Clear(SkiaSharp.SKColors.Transparent);using var paint=new SkiaSharp.SKPaint{Color=SkiaSharp.SKColors.White,StrokeWidth=2};using var path=new SkiaSharp.SKPath();SkiaLines.Draw(surface.Canvas,new[]{new SkiaSharp.SKPoint(4,8),new SkiaSharp.SKPoint(24,8),new SkiaSharp.SKPoint(4,20),new SkiaSharp.SKPoint(24,20)},2,paint,path);using var image=surface.Snapshot();using var pixels=SkiaSharp.SKBitmap.FromImage(image);if(pixels.GetPixel(10,8).Alpha==0||pixels.GetPixel(10,20).Alpha!=0)throw new Exception("Native line batch ignored its count");}
Console.WriteLine("PASS native Skia batch ABI draws only the visible buffer portion");
var results=new List<object>();
foreach(var (width,height,centered) in new[]{(1920,1080,false),(1440,900,false),(3840,2160,false),(1920,1080,true)})foreach(string mode in new[]{"legacy","optimized","light","stars","mesh"}){
 if((mode=="stars"||mode=="mesh")&&(width!=1920||centered))continue;
 var settings=new Settings("benchmark-unused");settings.Values["bgshow"]=System.Text.Json.Nodes.JsonValue.Create(mode=="stars"||mode=="mesh");settings.Values["clockshow"]=System.Text.Json.Nodes.JsonValue.Create(mode=="stars"||mode=="mesh");settings.BackgroundStyle=mode=="mesh"?"Malla":"Estrellas";settings.Values["rotation"]=System.Text.Json.Nodes.JsonValue.Create(0d);settings.Values["deformation"]=System.Text.Json.Nodes.JsonValue.Create(0d);settings.Values["pulse"]=System.Text.Json.Nodes.JsonValue.Create(0d);settings.RenderProfile=mode=="light"?"Ligero":"Calidad";if(centered){settings.Values["sphereX"]=System.Text.Json.Nodes.JsonValue.Create(50d);settings.Values["sphereY"]=System.Text.Json.Nodes.JsonValue.Create(50d);}var spectrum=new Spectrum();var media=new MediaService(settings);
 var pcm=Enumerable.Range(0,24000).Select(i=>(float)(.4*Math.Sin(2*Math.PI*110*i/48000)+.15*Math.Sin(2*Math.PI*1000*i/48000))).ToArray();spectrum.Push(pcm,pcm.Length,1,48000);
 Control view=mode=="legacy"?new LegacyWallpaperView(settings,spectrum,media):new WallpaperView(settings,spectrum,media);view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));
 using var bitmap=new RenderTargetBitmap(new PixelSize(width,height));for(int i=0;i<12;i++){spectrum.Push(pcm,pcm.Length,1,48000);if(view is WallpaperView v)v.Advance(i/165d);view.InvalidateVisual();bitmap.Render(view);}
 GC.Collect();long allocated=0;double elapsed=0;const int count=80;
 for(int i=0;i<count;i++){spectrum.Push(pcm,pcm.Length,1,48000);long before=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();if(view is WallpaperView v)v.Advance((i+12)/165d);view.InvalidateVisual();bitmap.Render(view);elapsed+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;allocated+=GC.GetAllocatedBytesForCurrentThread()-before;}
 var result=new{implementation=mode,width,height,centered,frames=count,msPerFrame=elapsed/count,allocatedBytesPerFrame=allocated/count,actualFrames=view is WallpaperView n?n.Frames:((LegacyWallpaperView)view).Frames};results.Add(result);Console.WriteLine(JsonSerializer.Serialize(result));
 if((mode=="optimized"||mode=="stars"||mode=="mesh")&&width==1920&&!centered&&args.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);bitmap.Save(Path.ChangeExtension(args[0],mode=="optimized"?"png":mode+".png"),new PngBitmapEncoderOptions());}
 (view as IDisposable)?.Dispose();
}
var random=new Random(42);
foreach(int rate in new[]{44100,48000,96000,192000}){
 var signal=Enumerable.Range(0,rate).Select(_=>(float)(random.NextDouble()-.5)).ToArray();var legacy=new LegacySpectrum();var optimized=new Spectrum();legacy.Push(signal,signal.Length,2,rate);optimized.Push(signal,signal.Length,2,rate);double error=legacy.Snapshot().Bands.Zip(optimized.Snapshot().Bands,(a,b)=>Math.Abs(a-b)).Max();if(error>1e-9)throw new Exception("Packed stereo FFT differs from independent FFTs: "+error);Console.WriteLine($"PASS independent stereo FFT equivalence at {rate} Hz: error {error:E3}");
 foreach(string mode in new[]{"legacy-fft","optimized-fft"}){var watch=Stopwatch.StartNew();for(int i=0;i<8;i++){if(mode=="legacy-fft")legacy.Push(signal,signal.Length,2,rate);else optimized.Push(signal,signal.Length,2,rate);}watch.Stop();var r=new{implementation=mode,rate,msPerAudioSecond=watch.Elapsed.TotalMilliseconds/4};results.Add(r);Console.WriteLine(JsonSerializer.Serialize(r));}
}
if(args.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));}
