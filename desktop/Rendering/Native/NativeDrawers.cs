#if WINDOWS
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Size= Avalonia.Size;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.D3DCompiler;
using Vortice.Mathematics;
using D3Buffer=Vortice.Direct3D11.ID3D11Buffer;
namespace WirePulse;
internal interface INativeDrawer:IDisposable {void Draw(NativeTexture target,PixelSize pixels,Size logical,double scale,VectorFrame frame);}
internal sealed class D3DLineDrawer:INativeDrawer {
 static readonly Lazy<ReadOnlyMemory<byte>> vertexCode=new(()=>Compiler.Compile(shader,"vs","native-lines.hlsl","vs_5_0")),pixelCode=new(()=>Compiler.Compile(shader,"ps","native-lines.hlsl","ps_5_0"));
 readonly NativeDevice device;readonly ID3D11VertexShader vertex;readonly ID3D11PixelShader pixel;readonly ID3D11InputLayout layout;readonly ID3D11BlendState blend;readonly ID3D11RasterizerState rasterizer;readonly D3Buffer constants;D3Buffer? instances;int capacity;
 const string shader="""
 cbuffer View : register(b0) { float2 viewport; float scale; float padding; };
 struct Input { float4 ends:ENDS; float4 color:COLOR; float4 style:STYLE; };
 struct Output {float4 position:SV_POSITION;float4 color:COLOR;float2 local:TEXCOORD0;float2 extent:TEXCOORD1;};
 Output vs(Input i,uint id:SV_VertexID){
  const float2 corners[6]={float2(0,-1),float2(1,-1),float2(0,1),float2(0,1),float2(1,-1),float2(1,1)};
  float2 delta=i.ends.zw-i.ends.xy;float len=max(length(delta),0.0001);float2 direction=delta/len;float2 normal=float2(-direction.y,direction.x);
  float halfWidth=i.style.x*0.5,aa=1/max(scale,0.1);float2 c=corners[id];float along=lerp(-aa,len+aa,c.x);float across=c.y*(halfWidth+aa);float2 p=i.ends.xy+direction*along+normal*across;
  Output o;o.position=float4(p/viewport*float2(2,-2)+float2(-1,1),0,1);o.color=i.color;o.local=float2(along,across);o.extent=float2(len,halfWidth);return o;
 }
 float4 ps(Output i):SV_TARGET {float2 d=float2(max(-i.local.x,i.local.x-i.extent.x),abs(i.local.y)-i.extent.y);float distance=max(d.x,d.y);float alpha=saturate(0.5-distance*scale)*i.color.a;return float4(i.color.rgb*alpha,alpha);}
 """;
 public D3DLineDrawer(NativeDevice device){this.device=device;var vs=vertexCode.Value;var ps=pixelCode.Value;vertex=device.Device.CreateVertexShader(vs.Span);pixel=device.Device.CreatePixelShader(ps.Span);layout=device.Device.CreateInputLayout(new[]{new Vortice.Direct3D11.InputElementDescription("ENDS",0,Format.R32G32B32A32_Float,0,0,InputClassification.PerInstanceData,1),new Vortice.Direct3D11.InputElementDescription("COLOR",0,Format.R32G32B32A32_Float,16,0,InputClassification.PerInstanceData,1),new Vortice.Direct3D11.InputElementDescription("STYLE",0,Format.R32G32B32A32_Float,32,0,InputClassification.PerInstanceData,1)},vs.Span);blend=device.Device.CreateBlendState(Vortice.Direct3D11.BlendDescription.AlphaBlend);rasterizer=device.Device.CreateRasterizerState(new RasterizerDescription{FillMode=Vortice.Direct3D11.FillMode.Solid,CullMode=CullMode.None,DepthClipEnable=true});constants=device.Device.CreateBuffer(new BufferDescription{ByteWidth=16,BindFlags=BindFlags.ConstantBuffer,Usage=ResourceUsage.Dynamic,CPUAccessFlags=CpuAccessFlags.Write});}
 public unsafe void Draw(NativeTexture target,PixelSize pixels,Size logical,double scale,VectorFrame frame){var c=device.Context;c.ClearRenderTargetView(target.Target,new Color4(0,0,0,0));if(frame.Count==0)return;if(frame.Count>capacity){instances?.Dispose();capacity=Math.Max(32768,frame.Count*2);instances=device.Device.CreateBuffer(new BufferDescription{ByteWidth=(uint)(capacity*48),BindFlags=BindFlags.VertexBuffer,Usage=ResourceUsage.Dynamic,CPUAccessFlags=CpuAccessFlags.Write});}var mapped=c.Map(instances!,0,MapMode.WriteDiscard,Vortice.Direct3D11.MapFlags.None);try{frame.Lines.AsSpan(0,frame.Count).CopyTo(new Span<NativeLine>((void*)mapped.DataPointer,frame.Count));}finally{c.Unmap(instances!,0);}mapped=c.Map(constants,0,MapMode.WriteDiscard,Vortice.Direct3D11.MapFlags.None);*(Vector4*)mapped.DataPointer=new((float)logical.Width,(float)logical.Height,(float)scale,0);c.Unmap(constants,0);c.OMSetRenderTargets(target.Target);c.OMSetBlendState(blend);c.RSSetState(rasterizer);c.RSSetViewport(0,0,pixels.Width,pixels.Height);c.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);c.IASetInputLayout(layout);c.IASetVertexBuffer(0,instances!,48);c.VSSetShader(vertex);c.VSSetConstantBuffer(0,constants);c.PSSetShader(pixel);c.PSSetConstantBuffer(0,constants);c.DrawInstanced(6,(uint)frame.Count,0,0);c.OMSetRenderTargets(Array.Empty<ID3D11RenderTargetView>());}
 public void Dispose(){instances?.Dispose();constants.Dispose();rasterizer.Dispose();blend.Dispose();layout.Dispose();pixel.Dispose();vertex.Dispose();}
}
internal sealed class D2DLineDrawer:INativeDrawer {
 readonly ID2D1Factory1 factory;readonly ID2D1Device device;readonly ID2D1DeviceContext context;readonly ID2D1SolidColorBrush brush;
 readonly Dictionary<NativeTexture,ID2D1Bitmap1> targets=[];
 public D2DLineDrawer(NativeDevice native){factory=Vortice.Direct2D1.D2D1.D2D1CreateFactory<ID2D1Factory1>();using var dxgi=native.Device.QueryInterface<IDXGIDevice>();device=factory.CreateDevice(dxgi);context=device.CreateDeviceContext();brush=context.CreateSolidColorBrush(new Color4(1,1,1,1));}
 public void Draw(NativeTexture target,PixelSize pixels,Size logical,double scale,VectorFrame frame){if(!targets.TryGetValue(target,out var bitmap)){// Resizes invalidate all previous swap textures.
  if(targets.Count>=2){foreach(var old in targets.Values)old.Dispose();targets.Clear();}
  using var surface=target.Texture.QueryInterface<IDXGISurface>();bitmap=context.CreateBitmapFromDxgiSurface(surface,new BitmapProperties1(new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm,Vortice.DCommon.AlphaMode.Premultiplied),96,96,BitmapOptions.Target|BitmapOptions.CannotDraw));targets[target]=bitmap;}
  context.Target=bitmap;context.Transform=Matrix3x2.CreateScale((float)scale);context.BeginDraw();context.Clear(new Color4(0,0,0,0));for(int i=0;i<frame.Count;){var first=frame.Lines[i];using var path=factory.CreatePathGeometry();using(var sink=path.Open()){do{var line=frame.Lines[i++];sink.BeginFigure(new(line.Ends.X,line.Ends.Y),FigureBegin.Hollow);sink.AddLine(new(line.Ends.Z,line.Ends.W));sink.EndFigure(FigureEnd.Open);}while(i<frame.Count&&frame.Lines[i].Color==first.Color&&frame.Lines[i].Style.X==first.Style.X);sink.Close();}brush.Color=new(first.Color.X,first.Color.Y,first.Color.Z,first.Color.W);context.DrawGeometry(path,brush,first.Style.X);}context.EndDraw().CheckError();context.Target=null;
 }
 public void Dispose(){foreach(var t in targets.Values)t.Dispose();brush.Dispose();context.Dispose();device.Dispose();factory.Dispose();}
}
internal sealed class NativeVisualizationControl(Func<VisualizerOptions> options,Func<SpectrumFrame> audio):GpuContentControl {
 readonly VectorFrame frame=new();INativeDrawer? drawer;
 protected override string ContentName=>RenderEngines.Requested;
 protected override void InitializeContent(){drawer=RenderEngines.Requested==RenderEngines.Direct2D?new D2DLineDrawer(Device):new D3DLineDrawer(Device);}
 protected override void RenderContent(NativeTexture target,PixelSize size,Size logical,double scale){frame.Build(logical.Width,logical.Height,System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency,options(),audio(),Paused);drawer!.Draw(target,size,logical,scale,frame);}
 protected override void FreeContent(){drawer?.Dispose();drawer=null;}
}
#endif
