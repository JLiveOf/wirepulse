#if WINDOWS
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;
namespace WirePulse;

internal sealed class NativeDevice:IDisposable {
 public readonly ID3D11Device Device;public readonly ID3D11DeviceContext Context;public readonly string Description;public readonly bool Software;
 public NativeDevice(byte[]? luid=null){using var factory=CreateDXGIFactory1<IDXGIFactory1>();IDXGIAdapter1? chosen=null;try{if(luid is{Length:8})for(uint i=0;factory.EnumAdapters1(i,out var adapter).Success;i++){if(adapter.Description1.Luid.LowPart==BitConverter.ToUInt32(luid,0) && adapter.Description1.Luid.HighPart==BitConverter.ToInt32(luid,4)){chosen=adapter;break;}adapter.Dispose();}
 var flags=DeviceCreationFlags.BgraSupport|DeviceCreationFlags.VideoSupport;var levels=new[]{FeatureLevel.Level_11_0};bool software=chosen!=null&&(chosen.Description1.Flags&AdapterFlags.Software)!=0;var result=D3D11CreateDevice(software?null:chosen,software?DriverType.Warp:chosen!=null?DriverType.Unknown:DriverType.Hardware,flags,levels,out ID3D11Device device,out ID3D11DeviceContext context);if(result.Failure)result=D3D11CreateDevice(null,DriverType.Warp,flags,levels,out device,out context);result.CheckError();Device=device!;Context=context!;using var dxgi=Device.QueryInterface<IDXGIDevice>();using var actual=dxgi.GetAdapter();Description=actual.Description.Description;using var adapterInfo=actual.QueryInterface<IDXGIAdapter1>();Software=(adapterInfo.Description1.Flags&AdapterFlags.Software)!=0;using var multi=Device.QueryInterface<ID3D11Multithread>();multi.SetMultithreadProtected(true);}finally{chosen?.Dispose();}}
 public NativeTexture CreateTexture(PixelSize size,ICompositionGpuInterop? interop,CompositionDrawingSurface? surface)=>new(this,size,interop,surface);
 public void Dispose(){Context.ClearState();Context.Dispose();Device.Dispose();}
}
internal sealed class NativeTexture:IDisposable {
 public readonly PixelSize Size;public readonly ID3D11Texture2D Texture;public readonly ID3D11RenderTargetView Target;readonly IDXGIKeyedMutex? mutex;readonly ICompositionImportedGpuImage? image;readonly CompositionDrawingSurface? surface;public Task LastPresent{get;private set;}=Task.CompletedTask;
 public NativeTexture(NativeDevice device,PixelSize size,ICompositionGpuInterop? interop,CompositionDrawingSurface? surface){Size=size;this.surface=surface;bool shared=interop!=null;Texture=device.Device.CreateTexture2D(new Texture2DDescription{Width=(uint)size.Width,Height=(uint)size.Height,MipLevels=1,ArraySize=1,Format=Format.B8G8R8A8_UNorm,SampleDescription=new SampleDescription(1,0),Usage=ResourceUsage.Default,BindFlags=BindFlags.RenderTarget|BindFlags.ShaderResource,MiscFlags=shared?ResourceOptionFlags.SharedKeyedMutex:ResourceOptionFlags.None});Target=device.Device.CreateRenderTargetView(Texture);if(shared){mutex=Texture.QueryInterface<IDXGIKeyedMutex>();using var resource=Texture.QueryInterface<IDXGIResource>();var handle=resource.SharedHandle;image=interop!.ImportImage(new PlatformHandle(handle,KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle),new PlatformGraphicsExternalImageProperties{Width=size.Width,Height=size.Height,Format=PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm});}}
 public unsafe bool Begin(){if(!LastPresent.IsCompleted)return false;if(mutex==null)return true;var pointer=mutex.NativePointer;var acquire=(delegate* unmanaged[Stdcall]<nint,ulong,uint,int>)(*(nint**)pointer)[8];return acquire(pointer,0,0)==0;}
 public Task Present(NativeDevice device){device.Context.Flush();if(mutex!=null){mutex.ReleaseSync(1);LastPresent=surface!.UpdateWithKeyedMutexAsync(image!,1,0);}return LastPresent;}
 public void Abort(){mutex?.ReleaseSync(0);}
 public void Dispose(){if(image is IAsyncDisposable disposable)_=disposable.DisposeAsync();Target.Dispose();mutex?.Dispose();Texture.Dispose();}
}
#endif
