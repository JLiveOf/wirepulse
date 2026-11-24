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
 let localBuild=BuildIdentity(version:"4.1.1",id:String(repeating:"a",count:40),sequence:100),newBuild=BuildIdentity(version:"4.1.1",id:String(repeating:"b",count:40),sequence:200)
 let builds=try JSONEncoder().encode(["macos":newBuild,"windows":localBuild])
 let hotfix=UpdateManager.select(data,current:"4.1.1",channel:"stable",builds:builds,build:localBuild)
 precondition(hotfix != nil)
 precondition(UpdateManager.select(data,current:"4.1.1",channel:"stable",builds:builds,build:newBuild)==nil)
 precondition(UpdateManager.select(data,current:"4.1.1",channel:"stable",skipped:hotfix!.identity,builds:builds,build:localBuild)==nil)
 precondition(UpdateManager.select(data,current:"4.1.1",channel:"stable",skipped:hotfix!.identity.replacingOccurrences(of:String(repeating:"a",count:64),with:String(repeating:"b",count:64)),builds:builds,build:localBuild) != nil)
 precondition(UpdateManager.select(data,current:"4.1.1",channel:"stable",builds:builds,build:BuildIdentity(version:"4.1.1",id:String(repeating:"c",count:40),sequence:300))==nil)
 precondition(UpdateManager.select(data,current:"4.1.1",channel:"stable",builds:Data("invalid".utf8),build:localBuild)==nil)
 print("PASS hotfix identity, no downgrade, no repeated offers and per-build skip")
 precondition(UpdateManager.resolveInstalled("4.2.0",.init(tag:"v4.2.0-beta.10",hash:"abc"),"abc")=="4.2.0-beta.10")
 precondition(UpdateManager.resolveInstalled("4.2.0",.init(tag:"v4.2.0-beta.10",hash:"abc"),"other")=="4.2.0")
 precondition(!UpdateManager.trusted("https://evil.example/update.zip") && !UpdateManager.trusted("http://github.com/JLiveOf/wirepulse/releases/download/v5/app.zip"))
 print("PASS native stable/beta selection, semantic versions, skipped releases and trusted URLs")
 for rate in [60,75,165,240]{var time=0.0;while time<2.137{time+=min(1/Double(rate),2.137-time)};let frame=TextMotion.frame("typewriter",time,1,12,4);precondition(frame.characters==2)}
 var item=SceneItem();item.kind="text";item.text="WirePulse";item.textSize=70;item.textBold=true;item.textItalic=true;item.textAnimation="float";item.textAlign="right";item.gradients["color"]=GradientSpec(enabled:true,end:"#0000ff",angle:135)
 let decoded=try JSONDecoder().decode(SceneItem.self,from:JSONEncoder().encode(item));precondition(decoded==item)
 var legacy=try JSONDecoder().decode(SceneItem.self,from:Data("{\"kind\":\"text\",\"text\":\"Legacy\"}".utf8));legacy.cleanEffects();precondition(legacy.textAnimation=="none" && legacy.textSize==0 && legacy.gradients.isEmpty)
 print("PASS native text timing, formatting/gradient serialization and legacy scenes")
 let fm=FileManager.default,temp=fm.temporaryDirectory.appendingPathComponent("wirepulse-swap-"+UUID().uuidString);try fm.createDirectory(at:temp,withIntermediateDirectories:true);defer{try? fm.removeItem(at:temp)}
 for rollback in [false,true]{
  let root=temp.appendingPathComponent(String(rollback)),target=root.appendingPathComponent("WirePulse ' quoted; safe.app"),incoming=root.appendingPathComponent("incoming.app"),backup=root.appendingPathComponent("backup.app")
  let fake = #"""
#!/bin/sh
dir=$(dirname "$0")
printf '%s' "$2" > "$dir/recorded"
"""#
  for folder in [target,incoming]{let bin=folder.appendingPathComponent("Contents/MacOS");try fm.createDirectory(at:bin,withIntermediateDirectories:true);let file=bin.appendingPathComponent("WirePulse");try (fake+"\n").write(to:file,atomically:true,encoding:.utf8);try fm.setAttributes([.posixPermissions:0o755],ofItemAtPath:file.path)}
  try Data("old".utf8).write(to:target.appendingPathComponent("old"));try Data("new".utf8).write(to:incoming.appendingPathComponent("new"));if rollback{try fm.removeItem(at:incoming)}
  let script=root.appendingPathComponent("install.sh");try UpdateManager.swapScript(target:target,incoming:incoming,backup:backup,pid:Int32.max,tag:"v4.1.0-beta.1",opener:"/usr/bin/true").write(to:script,atomically:true,encoding:.utf8)
  try LayoutPackage.run("/bin/sh",[script.path])
  if rollback{precondition(fm.fileExists(atPath:target.appendingPathComponent("old").path))}else{precondition(fm.fileExists(atPath:backup.appendingPathComponent("old").path));let recorded=try String(contentsOf:target.appendingPathComponent("Contents/MacOS/recorded"),encoding:.utf8);precondition(recorded=="v4.1.0-beta.1")}
 }

 print("PASS native updater atomic replacement, rollback, backup and quoted paths")
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
 let config=Configuration(profile:"gradient-test-"+UUID().uuidString,persistent:false),analyzer=SpectrumAnalyzer()
 config.values["sphere"]=1;config.values["spectrum"]=0;config.values["bgshow"]=0;config.values["sphereX"]=50;config.values["sphereY"]=50;config.values["radius"]=35;config.values["glow"]=0;config.renderProfile="Calidad";config.colors["wirecolor"]="#00ff0000";config.gradients["wirecolor"]=GradientSpec(enabled:true,end:"#0000ff",angle:0)
 guard let device=MTLCreateSystemDefaultDevice() else{throw SceneAssets.failure("Metal unavailable in gradient test")}
 let renderer=try WallpaperRenderer(config:config,analyzer:analyzer,device:device),metalImage=temp.appendingPathComponent("metal-gradient.png")
 try renderer.renderPreview(to:metalImage)
 guard let pixels=NSBitmapImageRep(data:try Data(contentsOf:metalImage)) else{throw SceneAssets.failure("Metal preview unavailable")}
 var colored=0;for y in stride(from:0,to:pixels.pixelsHigh,by:2){for x in stride(from:0,to:pixels.pixelsWide,by:2){if let c=pixels.colorAt(x:x,y:y)?.usingColorSpace(.deviceRGB),c.blueComponent>0.1{colored+=1}}}
 precondition(colored>100,"Metal gradient discarded the opaque endpoint of a transparent start")
 print("PASS Metal gradient render with transparent start: \(colored) blue pixels")

}
