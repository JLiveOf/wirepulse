import AppKit
import SwiftUI
import simd

struct GradientSpec:Codable,Equatable {
 var enabled=false,end="#007aff",angle=0.0
 func cleaned()->GradientSpec{GradientSpec(enabled:enabled,end:NSColor(hex:end).hex,angle:angle.isFinite ? angle.truncatingRemainder(dividingBy:360):0)}
 func points(_ w:Double,_ h:Double)->(CGPoint,CGPoint){let a=angle * .pi/180,x=cos(a),y=sin(a),span=abs(x)*w+abs(y)*h;return(CGPoint(x:w/2-x*span/2,y:h/2-y*span/2),CGPoint(x:w/2+x*span/2,y:h/2+y*span/2))}
 func sample(_ start:SIMD4<Float>,_ x:Double,_ y:Double,_ w:Double,_ h:Double)->SIMD4<Float>{let a=angle * .pi/180,dx=cos(a),dy=sin(a),t=Float(min(1,max(0,0.5+((x-w/2)*dx+(y-h/2)*dy)/max(0.0001,abs(dx)*w+abs(dy)*h))));return start+(NSColor(hex:end).rgba-start)*t}
 func shape(_ start:NSColor)->AnyShapeStyle{guard enabled else{return AnyShapeStyle(Color(nsColor:start))};let(a,b)=points(1,1);return AnyShapeStyle(LinearGradient(colors:[Color(nsColor:start),Color(nsColor:NSColor(hex:end))],startPoint:UnitPoint(x:a.x,y:a.y),endPoint:UnitPoint(x:b.x,y:b.y)))}
}
enum TextMotion {
 static let modes=["none","float","pulse","typewriter","scroll"]
 static func frame(_ mode:String,_ time:Double,_ speed:Double,_ distance:Double,_ count:Int)->(x:Double,y:Double,scale:Double,opacity:Double,characters:Int){let t=max(0,time)*min(5,max(0.1,speed)),wave=sin(t * .pi*2/3);switch mode{case "float":return(0,wave*distance,1,1,count);case "pulse":return(0,0,1+0.05*wave,0.75+0.25*(wave+1)/2,count);case "scroll":return((1-(t/6).truncatingRemainder(dividingBy:1)*2)*distance,0,1,1,count);case "typewriter":return(0,0,1,1,min(count,max(0,Int((t/5).truncatingRemainder(dividingBy:1)*1.5*Double(count)))));default:return(0,0,1,1,count)}}
}
