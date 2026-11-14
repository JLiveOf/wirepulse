import AppKit
import MetalKit
import simd

struct WireVertex { var position:SIMD2<Float>; var color:SIMD4<Float> }
struct MeshPoint {var x:Float;var y:Float;var z:Float;var bin:Int;var phase:Float}
final class WallpaperRenderer:NSObject,MTKViewDelegate {
 let config:Configuration;let analyzer:SpectrumAnalyzer;let media:NowPlaying?
 let device:MTLDevice;private let commandQueue:MTLCommandQueue;private let pipeline:MTLRenderPipelineState
 private var smooth=[Float](repeating:0,count:64),peaks=[Float](repeating:0,count:64)
 private var mesh=[MeshPoint](),edges=[(Int,Int)](),density=0
 var backgroundPulse=0.0
 var spaceNodes=[SIMD2<Float>]()
 var backgroundMotion=BackgroundMotion()
 let stars=StarMotion()
 private var vertices=[WireVertex](),angle:Float=0,last=ProcessInfo.processInfo.systemUptime
 private var buffers=[MTLBuffer?](repeating:nil,count:3),bufferIndex=0
 private let inFlight=DispatchSemaphore(value:3)
 var frameCount:UInt64=0
 init(config:Configuration,analyzer:SpectrumAnalyzer,device:MTLDevice,media:NowPlaying?=nil)throws{
  self.config=config;self.analyzer=analyzer;self.device=device;self.media=media
  guard let q=device.makeCommandQueue() else{throw NSError(domain:"WirePulse",code:2,userInfo:[NSLocalizedDescriptionKey:"Metal no pudo crear una cola de dibujo"])};commandQueue=q
  let source="""
  #include <metal_stdlib>
  using namespace metal;
  struct V {float2 position;float4 color;};
  struct Out {float4 position [[position]];float4 color;};
  vertex Out wire_vertex(const device V* v [[buffer(0)]],uint i [[vertex_id]]) {Out o;o.position=float4(v[i].position,0,1);o.color=v[i].color;return o;}
  fragment float4 wire_fragment(Out in [[stage_in]]) {return in.color;}
  """
  let lib=try device.makeLibrary(source:source,options:nil),descriptor=MTLRenderPipelineDescriptor()
  descriptor.vertexFunction=lib.makeFunction(name:"wire_vertex");descriptor.fragmentFunction=lib.makeFunction(name:"wire_fragment");descriptor.colorAttachments[0].pixelFormat = .bgra8Unorm
  let a=descriptor.colorAttachments[0]!;a.isBlendingEnabled=true;a.sourceRGBBlendFactor = .sourceAlpha;a.destinationRGBBlendFactor = .oneMinusSourceAlpha;a.sourceAlphaBlendFactor = .one;a.destinationAlphaBlendFactor = .oneMinusSourceAlpha
  pipeline=try device.makeRenderPipelineState(descriptor:descriptor)
  super.init();vertices.reserveCapacity(100000)
 }
 func mtkView(_ view:MTKView,drawableSizeWillChange size:CGSize){}
 func draw(in view:MTKView){
  let desired=config.on("unlimitedfps") ? (view.window?.screen?.maximumFramesPerSecond ?? 60):Int(config.n("fps"));if view.preferredFramesPerSecond != desired{view.preferredFramesPerSecond=desired}
  let scale=(view.window?.backingScaleFactor ?? 2)*config.n("quality"),size=CGSize(width:view.bounds.width*scale,height:view.bounds.height*scale)
  if view.drawableSize != size{view.drawableSize=size}
  guard let descriptor=view.currentRenderPassDescriptor,let drawable=view.currentDrawable else{return}
  let now=ProcessInfo.processInfo.systemUptime;let dt=min(0.1,max(0,now-last));last=now
  build(width:Float(view.bounds.width),height:Float(view.bounds.height),time:Float(now),dt:Float(dt))
  let bg=config.color("backgroundcolor").rgba
  descriptor.colorAttachments[0].clearColor=config.backgroundPath.isEmpty ? MTLClearColor(red:Double(bg.x),green:Double(bg.y),blue:Double(bg.z),alpha:1):MTLClearColorMake(0,0,0,0)
  guard let command=encode(descriptor) else{return};command.present(drawable);command.commit();frameCount+=1
 }
 private func encode(_ descriptor:MTLRenderPassDescriptor)->MTLCommandBuffer?{
  inFlight.wait();guard let command=commandQueue.makeCommandBuffer(),let encoder=command.makeRenderCommandEncoder(descriptor:descriptor) else{inFlight.signal();return nil}
  let byteCount=vertices.count*MemoryLayout<WireVertex>.stride
  if byteCount>0{
   let slot=bufferIndex;bufferIndex=(bufferIndex+1)%3
   if (buffers[slot]?.length ?? 0)<byteCount{buffers[slot]=device.makeBuffer(length:max(byteCount,1024*1024),options:.storageModeShared)}
   guard let buffer=buffers[slot] else{encoder.endEncoding();inFlight.signal();return nil}
   vertices.withUnsafeBytes{raw in buffer.contents().copyMemory(from:raw.baseAddress!,byteCount:byteCount)}
   encoder.setRenderPipelineState(pipeline);encoder.setVertexBuffer(buffer,offset:0,index:0);encoder.drawPrimitives(type:.triangle,vertexStart:0,vertexCount:vertices.count)
  }
  encoder.endEncoding();command.addCompletedHandler{[inFlight] _ in inFlight.signal()};return command
 }
 private func makeMesh(_ n:Int){
  density=n;mesh.removeAll(keepingCapacity:true);edges.removeAll(keepingCapacity:true);let m=n*2
  for j in 0...n {for i in 0..<m{
   let lat=Float(j)/Float(n)*Float.pi,lon=Float(i)/Float(m)*Float.pi*2
   mesh.append(MeshPoint(x:sin(lat)*cos(lon),y:cos(lat),z:sin(lat)*sin(lon),bin:min(63,Int((1-abs(cos(lat)))*46+Float(i)/Float(m)*17)),phase:Float(i)*2.399+Float(j)*0.7))
   let a=j*m+i;if j<n{edges.append((a,a+m));edges.append((a,(j+1)*m+(i+1)%m))};edges.append((a,j*m+(i+1)%m))
  }}
 }
 var width:Float=1,height:Float=1
 private func point(_ x:Float,_ y:Float)->SIMD2<Float>{SIMD2(x/width*2-1,1-y/height*2)}
 private func triangle(_ a:SIMD2<Float>,_ b:SIMD2<Float>,_ c:SIMD2<Float>,_ color:SIMD4<Float>){vertices.append(WireVertex(position:point(a.x,a.y),color:color));vertices.append(WireVertex(position:point(b.x,b.y),color:color));vertices.append(WireVertex(position:point(c.x,c.y),color:color))}
 func rect(_ x:Float,_ y:Float,_ w:Float,_ h:Float,_ color:SIMD4<Float>){guard w>0,h>0 else{return};let a=SIMD2(x,y),b=SIMD2(x+w,y),c=SIMD2(x,y+h),d=SIMD2(x+w,y+h);triangle(a,b,c,color);triangle(c,b,d,color)}
 func line(_ a:SIMD2<Float>,_ b:SIMD2<Float>,_ thickness:Float,_ color:SIMD4<Float>){
  if max(a.x,b.x)<0 || min(a.x,b.x)>width || max(a.y,b.y)<0 || min(a.y,b.y)>height{return}
  let delta=b-a,length=simd_length(delta);guard length>0.0001 else{return};let n=SIMD2(-delta.y,delta.x)*(thickness*0.5/length)
  triangle(a-n,b-n,a+n,color);triangle(a+n,b-n,b+n,color)
 }
 private func build(width:Float,height:Float,time:Float,dt:Float){
  self.width=max(1,width);self.height=max(1,height);vertices.removeAll(keepingCapacity:true)
  let snapshot=analyzer.snapshot(),decay=pow(Float(config.n("smoothing")),dt*60)
  for i in 0..<64{let input=snapshot.age<0.3 && !config.paused ? snapshot.bands[i]:0;let v=min(1,max(0,input-Float(config.n("gate")))*Float(config.n("sensitivity"))*((snapshot.ranges.indices.contains(i) && snapshot.ranges[i].upperBound>20 && snapshot.ranges[i].lowerBound<160) ? Float(config.n("bass")):1));smooth[i]=v+(smooth[i]-v)*decay;if smooth[i]<0.0001{smooth[i]=0}}
  if !config.backgroundPath.isEmpty{rect(0,0,width,height,SIMD4(0,0,0,Float(config.n("dim")/100)))}
  let beat=snapshot.age<0.8 && !config.paused ? snapshot.beat*exp(-snapshot.age*7):0
  let ciderBpm=media?.tempoBpm ?? 0
  drawSpace(time:time,dt:dt,beat:beat,bpm:ciderBpm>0 ? ciderBpm:snapshot.bpm)
  let bassIndices=snapshot.ranges.indices.filter{snapshot.ranges[$0].upperBound>20 && snapshot.ranges[$0].lowerBound<160};let low=bassIndices.reduce(Float(0)){$0+smooth[$1]}/Float(max(1,bassIndices.count))
  if !config.paused{angle += dt*Float(config.n("rotation"))}
  if config.on("sphere"){
   let n=Int(config.n("density"));if n != density{makeMesh(n)}
   let radius=height*Float(config.n("radius")/100)*(1+low*Float(config.n("pulse"))),cx=width*Float(config.n("sphereX")/100),cy=height*Float(config.n("sphereY")/100),full=config.on("fullmesh"),deformation=Float(config.n("deformation"))
   let ca=cos(angle),sa=sin(angle),ct=cos(Float(0.23)),st=sin(Float(0.23))
   let projected=mesh.map{p->SIMD3<Float> in let e=smooth[p.bin],ripple=sin(p.phase+time*1.7)*e,d:Float=1+deformation*(e*(full ? 0.22:0.045)+ripple*(full ? 0.16:0.025));let x=(p.x*ca-p.z*sa)*d,y=p.y*d,z=(p.x*sa+p.z*ca)*d;return SIMD3(cx+x*radius,cy+(y*ct-z*st)*radius,z*ct+y*st)}
   let base=config.color("wirecolor").rgba,opacity=Float(config.n("opacity")),thickness=Float(config.n("linewidth")),glow=Float(config.n("glow"))
   for pass in 0...1{
    var c=base;c.w=opacity*(pass==1 ? 0.85:0.24)
    for (a,b) in edges{let p=projected[a],q=projected[b],z=(p.z+q.z)*0.5;if !full && abs(z)>0.25{continue};if (z>0) != (pass==1){continue};let aa=SIMD2(p.x,p.y),bb=SIMD2(q.x,q.y)
     if glow>0{var halo=c;halo.w *= 0.06;line(aa,bb,thickness+glow,halo);halo.w *= 1.5;line(aa,bb,thickness+glow*0.4,halo)}
     line(aa,bb,thickness,c)
    }
   }
   if !full{var c=base;c.w=opacity*0.48;for j in 0..<14{var previous:SIMD2<Float>?;for i in 0...48{let a=Float(i)/48*Float.pi*2,radial=radius*(0.48+Float(j)*0.007+smooth[i%48]*deformation*0.42),twist=a+sin(a*3+time*0.3+Float(j)*0.13)*0.09,p=SIMD2(cx+cos(twist)*radial,cy+sin(twist)*radial*0.94);if let prev=previous{line(prev,p,thickness,c)};previous=p}}}
  }
  if config.on("spectrum"){
   let x=width*Float(config.n("spectrumX")/100),y=height*Float(config.n("spectrumY")/100),h=height*Float(config.n("spectrumHeight")/100),maximum=width*Float(config.n("spectrumWidth")/100),row=h/64,step=max(4,width/240),barHeight=max(1,row*0.68)
   for i in 0..<64{let length=smooth[63-i]*maximum;peaks[i]=max(length,peaks[i]-dt*maximum*0.25)
    if length>0{var offset:Float=0;while offset<length{let f=offset/max(1,length);let color:SIMD4<Float>;if f<0.4{let t=f/0.4;color=SIMD4(0.34+0.38*t,1-0.14*t,0,1)}else if f<0.75{let t=(f-0.4)/0.35;color=SIMD4(0.72+0.28*t,0.86-0.47*t,0,1)}else{let t=(f-0.75)/0.25;color=SIMD4(1,0.39*(1-t),0.16*t,1)}
     rect(x+offset,y+Float(i)*row,min(config.on("segmented") ? step-1:step,length-offset),barHeight,color);offset+=step
    }}
    if peaks[i]>3{rect(x+peaks[i],y+Float(i)*row,1,barHeight,SIMD4(1,0.15,0.2,0.45))}
   }
  }
 }
 func renderPreview(to url:URL)throws{
  width=1920;height=1080;build(width:width,height:height,time:1,dt:1/60)
  let td=MTLTextureDescriptor.texture2DDescriptor(pixelFormat:.bgra8Unorm,width:1920,height:1080,mipmapped:false);td.usage=[.renderTarget];td.storageMode = .shared
  guard let texture=device.makeTexture(descriptor:td) else{return};let pass=MTLRenderPassDescriptor();pass.colorAttachments[0].texture=texture;pass.colorAttachments[0].loadAction = .clear;pass.colorAttachments[0].storeAction = .store;let bg=config.color("backgroundcolor").rgba;pass.colorAttachments[0].clearColor=MTLClearColorMake(Double(bg.x),Double(bg.y),Double(bg.z),1)
  guard let command=encode(pass) else{return};command.commit();command.waitUntilCompleted()
  var bytes=[UInt8](repeating:0,count:1920*1080*4);texture.getBytes(&bytes,bytesPerRow:1920*4,from:MTLRegionMake2D(0,0,1920,1080),mipmapLevel:0)
  for i in stride(from:0,to:bytes.count,by:4){bytes.swapAt(i,i+2)}
  let rep=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:1920,pixelsHigh:1080,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:1920*4,bitsPerPixel:32)!
  bytes.withUnsafeBytes{rep.bitmapData!.update(from:$0.baseAddress!.assumingMemoryBound(to:UInt8.self),count:bytes.count)}
  try rep.representation(using:.png,properties:[:])!.write(to:url)
 }
}
