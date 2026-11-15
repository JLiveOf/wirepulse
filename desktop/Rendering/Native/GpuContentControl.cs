#if WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using System.Diagnostics;
using Vortice.Direct3D11;
namespace WirePulse;

/// Shared GPU textures, with bounded back pressure. Native video can use a CPU readback
/// when software composition is selected; visualizers retain Skia as the safe fallback.
internal abstract class GpuContentControl:Control,IDisposable {
 NativeDevice? device;NativeTexture[] textures=[];CompositionDrawingSurface? surface;CompositionSurfaceVisual? visual;Compositor? compositor;ICompositionGpuInterop? interop;WriteableBitmap? bitmap;ID3D11Texture2D? staging;bool disposed,queued,busy,initializing,failed;int slot;double heartbeat;bool paused;readonly Avalonia.Threading.DispatcherTimer idle;
 protected NativeDevice Device=>device!;protected virtual bool AllowReadback=>false;public bool Ready{get;private set;}public string Status{get;protected set;}="Inicializando";public RenderStatistics Statistics{get;}=new();public event Action? AvailabilityChanged;
 protected GpuContentControl(){idle=new(TimeSpan.FromMilliseconds(300),Avalonia.Threading.DispatcherPriority.Background,(_,_)=>{if(Environment.TickCount64/1000d-heartbeat>.8)SetPaused(true);});idle.Start();}
 protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e){base.OnAttachedToVisualTree(e);Initialize();}
 async void Initialize(){if(disposed||initializing||device!=null)return;initializing=true;try{var own=ElementComposition.GetElementVisual(this);if(own==null)return;compositor=own.Compositor;interop=await compositor.TryGetCompositionGpuInterop();if(disposed)return;if(interop!=null&&!interop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle))interop=null;if(interop==null&&!AllowReadback)throw new NotSupportedException("La API activa no permite compartir texturas Direct3D 11.");device=new NativeDevice(interop?.DeviceLuid);if(interop!=null){surface=compositor.CreateDrawingSurface();visual=compositor.CreateSurfaceVisual();visual.Surface=surface;visual.Size=new(Bounds.Width,Bounds.Height);ElementComposition.SetElementChildVisual(this,visual);}InitializeContent();Status=ContentName+" · "+device.Description+(interop==null?" · copia de compatibilidad":" · textura compartida GPU");Queue();}catch(Exception ex){Fail("Skia · alternativa: "+ex.Message);}finally{initializing=false;}}
 public virtual void Advance(double seconds,bool isPaused){heartbeat=Environment.TickCount64/1000d;SetPaused(isPaused);Queue();}
 void SetPaused(bool value){if(paused==value)return;paused=value;PauseContent(value);}
 protected bool Paused=>paused;
 void Queue(){if(disposed||failed||device==null||queued||busy||compositor==null||!IsEffectivelyVisible)return;queued=true;compositor.RequestCompositionUpdate(UpdateFrame);}
 async void UpdateFrame(){queued=false;if(disposed||failed||device==null||busy||!IsEffectivelyVisible)return;var top=TopLevel.GetTopLevel(this);if(top==null||Bounds.Width<1||Bounds.Height<1)return;busy=true;try{double scaling=top.RenderScaling;var size=PixelSize.FromSize(Bounds.Size,scaling);if(size.Width>16384||size.Height>16384)throw new InvalidOperationException("El área de dibujo supera el límite de la GPU.");if(textures.Length==0||textures[0].Size!=size){await Drain();if(disposed)return;foreach(var t in textures)t.Dispose();textures=[device.CreateTexture(size,interop,surface),device.CreateTexture(size,interop,surface)];staging?.Dispose();staging=null;bitmap?.Dispose();bitmap=null;}if(disposed)return;var texture=textures[slot++%textures.Length];if(!texture.Begin())return;long start=Stopwatch.GetTimestamp();try{RenderContent(texture,size,Bounds.Size,scaling);if(interop!=null){visual!.Size=new(Bounds.Width,Bounds.Height);await texture.Present(device);}else Readback(texture);}catch{texture.Abort();throw;}if(disposed)return;Statistics.Backend=Status;Statistics.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);if(!Ready){Ready=true;AvailabilityChanged?.Invoke();}if(interop==null)InvalidateVisual();}catch(Exception ex){Ready=false;Fail("Skia · alternativa: "+ex.Message);}finally{busy=false;}}
 unsafe void Readback(NativeTexture texture){var size=texture.Size;if(staging==null){var desc=texture.Texture.Description;desc.BindFlags=BindFlags.None;desc.MiscFlags=ResourceOptionFlags.None;desc.Usage=ResourceUsage.Staging;desc.CPUAccessFlags=CpuAccessFlags.Read;staging=device!.Device.CreateTexture2D(desc);bitmap=new WriteableBitmap(size,new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Premul);}device!.Context.CopyResource(staging,texture.Texture);var data=device.Context.Map(staging,0,MapMode.Read,MapFlags.None);try{using var target=bitmap!.Lock();for(int row=0;row<size.Height;row++)Buffer.MemoryCopy((byte*)data.DataPointer+row*data.RowPitch,(byte*)target.Address+row*target.RowBytes,target.RowBytes,size.Width*4);}finally{device.Context.Unmap(staging,0);}}
 public override void Render(DrawingContext context){if(bitmap!=null)context.DrawImage(bitmap,new Rect(bitmap.Size),new Rect(Bounds.Size));}
 protected void Fail(string message){failed=true;Ready=false;Status=message;if(visual!=null)visual.Opacity=0;AvailabilityChanged?.Invoke();InvalidateVisual();}
 protected abstract string ContentName{get;}protected abstract void InitializeContent();protected abstract void RenderContent(NativeTexture target,PixelSize size,Size logical,double scaling);protected virtual void PauseContent(bool value){}protected abstract void FreeContent();
 async Task Drain(){foreach(var t in textures)try{await t.LastPresent;}catch{}}
 protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e){base.OnDetachedFromVisualTree(e);Dispose();}
 public async void Dispose(){if(disposed)return;disposed=true;idle.Stop();ElementComposition.SetElementChildVisual(this,null);await Drain();FreeContent();foreach(var t in textures)t.Dispose();textures=[];surface?.Dispose();staging?.Dispose();bitmap?.Dispose();device?.Dispose();device=null;}
}
#endif
