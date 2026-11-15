#if WINDOWS
using Avalonia;
using System.Diagnostics;
using System.Text.Json;
using Vortice.Direct3D11;
using Vortice.DXGI;
namespace WirePulse;
internal static class NativeDrawSelfTest {
 public static unsafe int Run(){using var device=new NativeDevice();var settings=new Settings("native-draw-self-test").Preview();settings.Set("bgshow",false);settings.Set("spectrum",false);settings.Set("sphereX",50d);settings.Set("sphereY",50d);settings.Set("radius",40d);settings.RenderProfile="Calidad";var options=VisualizerOptions.Read(settings);var spectrum=new Spectrum();var pcm=Enumerable.Range(0,24000).Select(i=>(float)(.6*Math.Sin(2*Math.PI*110*i/48000))).ToArray();spectrum.Push(pcm,pcm.Length,1,48000);var frame=new VectorFrame();var size=new PixelSize(640,360);using var texture=device.CreateTexture(size,null,null);var desc=texture.Texture.Description;desc.BindFlags=BindFlags.None;desc.Usage=ResourceUsage.Staging;desc.CPUAccessFlags=CpuAccessFlags.Read;using var staging=device.Device.CreateTexture2D(desc);foreach(string name in new[]{RenderEngines.Direct3D,RenderEngines.Direct2D}){using INativeDrawer drawer=name==RenderEngines.Direct3D?new D3DLineDrawer(device):new D2DLineDrawer(device);var watch=Stopwatch.StartNew();for(int i=0;i<20;i++){frame.Build(640,360,i/60d,options,spectrum.Snapshot(),false);drawer.Draw(texture,size,new Size(640,360),1,frame);}device.Context.Flush();device.Context.CopyResource(staging,texture.Texture);var map=device.Context.Map(staging,0,MapMode.Read,Vortice.Direct3D11.MapFlags.None);int colored=0;try{for(int y=0;y<360;y++)for(int x=0;x<640;x++){byte* p=(byte*)map.DataPointer+y*map.RowPitch+x*4;if(p[2]>5&&p[3]>5)colored++;}}finally{device.Context.Unmap(staging,0);}if(colored<300)throw new Exception(name+" produced an empty native render target: "+colored);Console.WriteLine(JsonSerializer.Serialize(new{engine=name,adapter=device.Description,frames=20,lines=frame.Count,coloredPixels=colored,elapsedMs=watch.Elapsed.TotalMilliseconds}));}return 0;}
}
#endif
