import AppKit
import simd

extension WallpaperRenderer {
 private func seed(_ index:UInt32,_ salt:UInt32)->Double{var x=index^salt;x ^= x>>16;x = x &* 0x7feb352d;x ^= x>>15;x = x &* 0x846ca68b;x ^= x>>16;return Double(x&0xffffff)/16777216}
 private func wrap(_ value:Double,_ extent:Double)->Double{let v=value.truncatingRemainder(dividingBy:extent);return v<0 ? v+extent:v}
 func drawSpace(time:Float,dt:Float,beat:Double,bpm:Double){guard config.on("bgshow") else{return};let reactive=config.on("bgreact"),target=reactive ? beat:0; backgroundPulse+=(target-backgroundPulse)*(1-exp(-Double(dt)*18));let pulse=backgroundPulse
  backgroundMotion.advance(dt:Double(dt),base:config.n("bgspeed"),direction:config.n("bgdirection"),boost:reactive ? config.n("bgboost"):0,beat:pulse,bpm:bpm,paused:config.paused)
  var color=config.color("bgcolor").rgba;color.w=Float(min(1,config.n("bgopacity")*(1+pulse*0.8)));let thickness=Float(config.n("bgline"))
  if config.n("bgstyle")<0.5{let count=Int(config.n("bgdensity"));stars.advance(count:count,dt:Double(dt),speed:backgroundMotion.speed,direction:config.n("bgdirection"),paused:config.paused);for i in 0..<count{let p=stars.position(i,width:Double(width),height:Double(height)),r=Float(1.5+seed(UInt32(i),0xabcdef01)*2)*(1+Float(pulse)*0.5);for j in 0..<8{let a=StarMotion.ring[j],b=StarMotion.ring[j+1];line(SIMD2(Float(p.x)+Float(a.x)*r,Float(p.y)+Float(a.y)*r),SIMD2(Float(p.x)+Float(b.x)*r,Float(p.y)+Float(b.y)*r),thickness,color)}};return}
  let spacing=config.n("bgspacing"),cols=Int(ceil(Double(width)/spacing))+3,rows=Int(ceil(Double(height)/spacing))+3,startX=floor(-backgroundMotion.x/spacing)-1,startY=floor(-backgroundMotion.y/spacing)-1
  if spaceNodes.count != cols*rows{spaceNodes=[SIMD2<Float>](repeating:.zero,count:cols*rows)}
  for y in 0..<rows{for x in 0..<cols{let gx=startX+Double(x),gy=startY+Double(y),warp=spacing*0.08*pulse;spaceNodes[y*cols+x]=SIMD2(Float(gx*spacing+backgroundMotion.x+sin(gx*0.71+gy*0.93+Double(time)*0.3)*warp),Float(gy*spacing+backgroundMotion.y+cos(gx*0.83-gy*0.63+Double(time)*0.3)*warp))}}
  for y in 0..<(rows-1){for x in 0..<(cols-1){let p=spaceNodes[y*cols+x];line(p,spaceNodes[y*cols+x+1],thickness,color);line(p,spaceNodes[(y+1)*cols+x],thickness,color);line(p,spaceNodes[(y+1)*cols+x+1],thickness,color)}}
 }
}
