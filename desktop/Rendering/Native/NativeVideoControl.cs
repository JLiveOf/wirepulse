#if WINDOWS
using Avalonia;
using Avalonia.Threading;
using Vortice;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.MediaFoundation;
using static Vortice.MediaFoundation.MediaFactory;
namespace WirePulse;

/// Media Foundation decodes directly into the shared Direct3D texture. No encoding or browser capture.
internal sealed class NativeVideoControl(string path):GpuContentControl {
 static readonly object startupLock=new();static bool started;
 IMFMediaEngine? engine;IMFDXGIDeviceManager? manager;MFByteStream? stream;bool canPlay,freed;long timestamp;public long DecodedFrames{get;private set;}
 protected override bool AllowReadback=>true;
 protected override string ContentName=>"Vídeo · Media Foundation";
 protected override void InitializeContent(){lock(startupLock){if(!started){MFStartup().CheckError();started=true;}}
  manager=MFCreateDXGIDeviceManager();manager.ResetDevice(Device.Device).CheckError();using var factory=new IMFMediaEngineClassFactory();using var attributes=MFCreateAttributes(2);attributes.VideoOutputFormat=Format.B8G8R8A8_UNorm;attributes.DxgiManager=manager;
  engine=factory.CreateInstance(MediaEngineCreateFlags.None,attributes,(kind,p1,p2)=>Dispatcher.UIThread.Post(()=>{if(freed||engine==null)return;if(kind==MediaEngineEvent.CanPlay){canPlay=true;if(!Paused)engine.Play();}else if(kind==MediaEngineEvent.Error){Fail("No se pudo decodificar el vídeo. Comprueba el formato y los códecs de Windows.");}}));engine.Muted=true;engine.Volume=0;engine.Loop=true;stream=new MFByteStream(path);using var extended=engine.QueryInterface<IMFMediaEngineEx>();extended.SetSourceFromByteStream(stream,path);
 }
 protected override void PauseContent(bool paused){if(engine==null||!canPlay)return;if(paused)engine.Pause();else engine.Play();}
 protected override void RenderContent(NativeTexture target,PixelSize size,Size logical,double scaling){Device.Context.ClearRenderTargetView(target.Target,new Vortice.Mathematics.Color4(0,0,0,1));if(engine==null||!canPlay)return;if(engine.OnVideoStreamTick(out timestamp))DecodedFrames++;var natural=engine.NativeVideoSize;VideoNormalizedRect crop=new(){Left=0,Top=0,Right=1,Bottom=1};if(natural.Width>0&&natural.Height>0){double ratio=(double)natural.Width/natural.Height,wanted=(double)size.Width/size.Height;if(ratio>wanted){float fraction=(float)(wanted/ratio);crop.Left=(1-fraction)*.5f;crop.Right=1-crop.Left;}else{float fraction=(float)(ratio/wanted);crop.Top=(1-fraction)*.5f;crop.Bottom=1-crop.Top;}}
  using var surface=target.Texture.QueryInterface<IDXGISurface>();engine.TransferVideoFrame(surface,crop,new RawRect(0,0,size.Width,size.Height),null);
 }
 protected override void FreeContent(){freed=true;engine?.Shutdown();engine?.Dispose();engine=null;stream?.Dispose();stream=null;manager?.Dispose();manager=null;}
}
#endif
