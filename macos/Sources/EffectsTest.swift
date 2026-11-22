import AppKit
import MetalKit

func testEffects(output:String?)throws {
 precondition(ReleaseVersion("4.1.0")!>ReleaseVersion("4.1.0-beta.10")! && ReleaseVersion("4.1.0-beta.10")!>ReleaseVersion("4.1.0-beta.2")!)
 func release(_ tag:String,_ beta:Bool=false,_ draft:Bool=false)->[String:Any]{["tag_name":tag,"prerelease":beta,"draft":draft,"assets":[["name":"WirePulse-\(tag)-macOS-arm64.zip","browser_download_url":"https://github.com/JLiveOf/wirepulse/releases/download/\(tag)/WirePulse.zip","size":123,"digest":"sha256:"+String(repeating:"a",count:64)]]]}
 let data=try JSONSerialization.data(withJSONObject:[release("v4.1.1"),release("v4.2.0-beta.2",true),release("v4.2.0-beta.10",true),release("v5.0.0",false,true)])
 precondition(UpdateManager.select(data,current:"4.1.0",channel:"stable")?.tag=="v4.1.1")
 precondition(UpdateManager.select(data,current:"4.1.0",channel:"beta")?.tag=="v4.2.0-beta.10")
 precondition(UpdateManager.select(data,current:"4.1.1",channel:"stable")==nil)
 precondition(UpdateManager.select(data,current:"4.1.0",channel:"stable",skipped:"4.1.1")==nil)
 precondition(UpdateManager.resolveInstalled("4.2.0",.init(tag:"v4.2.0-beta.10",hash:"abc"),"abc")=="4.2.0-beta.10")
 precondition(UpdateManager.resolveInstalled("4.2.0",.init(tag:"v4.2.0-beta.10",hash:"abc"),"other")=="4.2.0")
 precondition(!UpdateManager.trusted("https://evil.example/update.zip") && !UpdateManager.trusted("http://github.com/JLiveOf/wirepulse/releases/download/v5/app.zip"))
 print("PASS native stable/beta selection, semantic versions, skipped releases and trusted URLs")
 for rate in [60,75,165,240]{var time=0.0;while time<2.137{time+=min(1/Double(rate),2.137-time)};let frame=TextMotion.frame("typewriter",time,1,12,4);precondition(frame.characters==2)}
 var item=SceneItem();item.kind="text";item.text="WirePulse";item.textSize=70;item.textBold=true;item.textItalic=true;item.textAnimation="float";item.textAlign="right";item.gradients["color"]=GradientSpec(enabled:true,end:"#0000ff",angle:135)
 let decoded=try JSONDecoder().decode(SceneItem.self,from:JSONEncoder().encode(item));precondition(decoded==item)
 var legacy=try JSONDecoder().decode(SceneItem.self,from:Data("{\"kind\":\"text\",\"text\":\"Legacy\"}".utf8));legacy.cleanEffects();precondition(legacy.textAnimation=="none" && legacy.textSize==0 && legacy.gradients.isEmpty)
 print("PASS native text timing, formatting/gradient serialization and legacy scenes")
 _=NSApplication.shared
 item.textAlign="center";item.textItalic=false;item.textAnimation="none";item.color="#ff0000";item.gradients["color"]=GradientSpec(enabled:true,end:"#0000ff",angle:0)
 let view=AnimatedTextView(frame:CGRect(x:0,y:0,width:720,height:160));view.update(item)
 let image=NSImage(size:view.bounds.size,flipped:true){rect in NSColor.black.setFill();rect.fill();view.draw(rect);return true}
 guard let data=image.tiffRepresentation,let rep=NSBitmapImageRep(data:data) else{throw SceneAssets.failure("Text preview failed")}
 var red=0,blue=0
 for y in 0..<rep.pixelsHigh{for x in 0..<rep.pixelsWide{if let c=rep.colorAt(x:x,y:y)?.usingColorSpace(.deviceRGB){if c.redComponent>c.blueComponent+0.04 && c.redComponent>0.1{red+=1};if c.blueComponent>c.redComponent+0.04 && c.blueComponent>0.1{blue+=1}}}}
 precondition(red>100 && blue>100,"Gradient text did not render both colors: \(red), \(blue)")
 if let output=output{try rep.representation(using:.png,properties:[:])!.write(to:URL(fileURLWithPath:output))}
 print("PASS native gradient text pixels: red=\(red), blue=\(blue)")
}
