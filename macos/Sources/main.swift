import AppKit
import MetalKit
import ServiceManagement
import ImageIO

if let i=CommandLine.arguments.firstIndex(of:"--layout-self-test"),CommandLine.arguments.count>i+1{
 let fm=FileManager.default,temp=fm.temporaryDirectory.appendingPathComponent("wirepulse-layout-test-"+UUID().uuidString);try fm.createDirectory(at:temp,withIntermediateDirectories:true);defer{try? fm.removeItem(at:temp)}
 let package=try LayoutPackage.load(URL(fileURLWithPath:CommandLine.arguments[i+1]),destination:temp);let image=package.scene.items.first{$0.kind=="image"}!;precondition(image.x==0.125 && image.rotation==32 && fm.fileExists(atPath:image.asset));let widget=package.scene.items.first{$0.kind=="widget"}!;precondition(fm.fileExists(atPath:URL(fileURLWithPath:widget.asset).deletingLastPathComponent().appendingPathComponent("script.js").path));let config=Configuration(profile:UUID().uuidString,persistent:false);package.apply(config)
 let outputIndex=CommandLine.arguments.firstIndex(of:"--layout-output"),output=outputIndex.map{CommandLine.arguments[$0+1]} ?? temp.appendingPathComponent("roundtrip.wirepulse").path
 try LayoutPackage.export(to:URL(fileURLWithPath:output),config:config,size:CGSize(width:1920,height:1080));let round=try LayoutPackage.load(URL(fileURLWithPath:output),destination:temp);precondition(round.scene.items.count==package.scene.items.count && round.snap==package.snap);let originalBytes=try Data(contentsOf:URL(fileURLWithPath:image.asset)),roundBytes=try Data(contentsOf:URL(fileURLWithPath:round.scene.items.first{$0.kind=="image"}!.asset));precondition(originalBytes==roundBytes);try? fm.removeItem(at:temp);print("PASS native portable layout geometry, styles, image/video/widget resources and roundtrip");exit(0)
}
if let i=CommandLine.arguments.firstIndex(of:"--media-self-test"),CommandLine.arguments.count>i+1{
 for size in [CGSize(width:100,height:100),CGSize(width:1200,height:300),CGSize(width:300,height:1200)]{let viewport=CGRect(x:0,y:0,width:640,height:360),frame=BackgroundMedia.cover(size,in:viewport);precondition(frame.contains(viewport) && abs(frame.width/frame.height-size.width/size.height)<0.0001,"Background must fill without distortion or letterboxing")}
 _=NSApplication.shared;let media=BackgroundMedia(frame:CGRect(x:0,y:0,width:640,height:360));media.load(CommandLine.arguments[i+1]);let deadline=Date().addingTimeInterval(8);while media.playbackSeconds<0.2 && Date()<deadline{RunLoop.current.run(until:Date().addingTimeInterval(0.05))};precondition(media.hasVideo && media.playbackSeconds>=0.2,"Native video did not start");media.suspend(true);let before=media.playbackSeconds;RunLoop.current.run(until:Date().addingTimeInterval(0.25));precondition(abs(media.playbackSeconds-before)<0.1,"Native video did not pause");media.suspend(false);RunLoop.current.run(until:Date().addingTimeInterval(0.15));media.load("");precondition(!media.hasVideo,"Video resources retained after replacement");print("PASS AVFoundation background playback, pause, resume and replacement");exit(0)
}
if CommandLine.arguments.contains("--orb-render-self-test"){
 let config=Configuration(profile:UUID().uuidString,persistent:false)
 guard let device=MTLCreateSystemDefaultDevice() else{fatalError("Metal unavailable")}
 let renderer=try WallpaperRenderer(config:config,analyzer:SpectrumAnalyzer(),device:device)
 renderer.testOptimizedOrb();exit(0)
}
if let i=CommandLine.arguments.firstIndex(of:"--widget-self-test"),CommandLine.arguments.count>i+1{exit(testNativeWidget(path:CommandLine.arguments[i+1]))}
if let i=CommandLine.arguments.firstIndex(of:"--editor-preview"),CommandLine.arguments.count>i+1{
 _=NSApplication.shared;NSApp.setActivationPolicy(.accessory)
 let config=Configuration(profile:"editor-preview-"+UUID().uuidString,persistent:false),window=SceneEditorWindow(screen:NSScreen.main ?? NSScreen.screens[0],config:config,audio:SystemAudio(),media:NowPlaying());window.makeKeyAndOrderFront(nil)
 let previewDelay=CommandLine.arguments.firstIndex(of:"--preview-duration").flatMap{$0+1<CommandLine.arguments.count ? Double(CommandLine.arguments[$0+1]):nil} ?? 3
 Timer.scheduledTimer(withTimeInterval:min(300,max(3,previewDelay)),repeats:false){_ in let capture=Process();capture.executableURL=URL(fileURLWithPath:"/usr/sbin/screencapture");capture.arguments=["-x","-l",String(window.windowNumber),CommandLine.arguments[i+1]];try? capture.run();capture.waitUntilExit();if capture.terminationStatus != 0,let view=window.contentView,let rep=view.bitmapImageRepForCachingDisplay(in:view.bounds){view.cacheDisplay(in:view.bounds,to:rep);try? rep.representation(using:.png,properties:[:])?.write(to:URL(fileURLWithPath:CommandLine.arguments[i+1]))};window.orderOut(nil);exit(0)};NSApp.run();exit(0)
}
if CommandLine.arguments.contains("--editor-self-test"){
 _=NSApplication.shared
 let source=Configuration(profile:"editor-test-"+UUID().uuidString,persistent:false),size=CGSize(width:1000,height:600)
 source.scene=SceneDocument(items:[SceneItem(id:"text-fixture",kind:"text",name:"Texto",x:0.1,y:0.1,width:0.2,height:0.2)])
 let model=SceneEditorModel(source,size);model.selected="text-fixture";model.duplicate();precondition(model.document.items.count==2 && source.scene!.items.count==1);model.history(true);precondition(model.document.items.count==1);model.history(false);precondition(model.document.items.count==2);model.save();precondition(source.scene!.items.count==2)
 model.selected="text-fixture";let beforeGesture=model.document;model.beginGesture();for i in 1...30{model.change{$0.opacity=Double(i)/100}};model.endGesture();let afterGesture=model.document;model.history(true);precondition(model.document==beforeGesture,"Slider gesture generated multiple undo steps");model.history(false);precondition(model.document==afterGesture);model.history(true)
 model.document.items[1].visible=false;model.selected="text-fixture";model.snap=false;let canvas=EditorCanvasView(model),window=NSWindow(contentRect:CGRect(origin:.zero,size:size),styleMask:.borderless,backing:.buffered,defer:false);window.contentView=canvas
 func pointer(_ type:NSEvent.EventType,_ p:CGPoint)->NSEvent{NSEvent.mouseEvent(with:type,location:canvas.convert(p,to:nil),modifierFlags:[],timestamp:0,windowNumber:window.windowNumber,context:nil,eventNumber:0,clickCount:1,pressure:1)!}
 canvas.mouseDown(with:pointer(.leftMouseDown,CGPoint(x:160,y:110)));canvas.mouseDragged(with:pointer(.leftMouseDragged,CGPoint(x:235,y:150)));canvas.mouseUp(with:pointer(.leftMouseUp,CGPoint(x:235,y:150)));precondition(abs(model.item!.x-0.175)<0.001 && abs(model.item!.y-1.0/6)<0.001,"Native pointer did not move original text");let r=model.item!.rect(size);canvas.mouseDown(with:pointer(.leftMouseDown,CGPoint(x:r.maxX,y:r.maxY)));canvas.mouseDragged(with:pointer(.leftMouseDragged,CGPoint(x:r.maxX+40,y:r.maxY+24)));canvas.mouseUp(with:pointer(.leftMouseUp,CGPoint(x:r.maxX+40,y:r.maxY+24)));precondition(abs(model.item!.width-0.24)<0.001)
 let chrome=EditorHostingView(rootView:EditorChrome(model:model){_ in});chrome.frame=CGRect(origin:.zero,size:size);precondition(chrome.hitTest(CGPoint(x:400,y:300))==nil,"Chrome blocks canvas");precondition(chrome.hitTest(CGPoint(x:990,y:300)) != nil,"Layer list grip falls through to canvas");precondition(abs(canvas.nearest([397,497,597],[0,500,1000])-3)<0.001)
 let root=SceneRoot(frame:CGRect(origin:.zero,size:size),config:model.draft,audio:SystemAudio(),media:NowPlaying());root.refresh();precondition(root.subviews.count==2);let originalNodes=root.subviews;model.update{$0.x=0.3};root.refresh();precondition(root.subviews[0] === originalNodes[0] && root.subviews[1] === originalNodes[1],"Dragging recreated views")
 model.update{$0.rotation=90};root.refresh();let rotatedNode=root.subviews.first!,r0=model.item!.rect(size),tip=rotatedNode.convert(CGPoint(x:rotatedNode.bounds.maxX,y:rotatedNode.bounds.midY),to:root)
 precondition(abs(tip.x-r0.midX)<0.01 && abs(tip.y-(r0.midY+r0.width/2))<0.01,"Native object rotation disagrees with editor handles")
 let legacy=try! JSONDecoder().decode(SceneDocument.self,from:Data("{\"version\":1,\"items\":[{\"kind\":\"orb\"}]}".utf8));precondition(legacy.items[0].rotation==0,"Legacy scene failed without rotation field")
 precondition(DisplayPolicy.covers(CGRect(x:0,y:24,width:1000,height:550),CGRect(x:0,y:24,width:1000,height:550)));precondition(!DisplayPolicy.covers(CGRect(x:1000,y:0,width:1000,height:600),CGRect(x:0,y:0,width:1000,height:600)),"Coverage from another monitor pauses this display")
 let displayFrame=CGRect(x:-1920,y:100,width:1920,height:1080)
 for visible in [CGRect(x:-1920,y:180,width:1920,height:976),CGRect(x:-1800,y:100,width:1800,height:1056),CGRect(x:-1920,y:100,width:1800,height:1056)]{let controls=SceneEditorWindow.controlsFrame(screen:displayFrame,visible:visible);precondition(controls.offsetBy(dx:displayFrame.minX,dy:displayFrame.minY)==visible,"Editor controls overlap Dock/menu bar or ignore monitor origin")}
 let actualEditor=SceneEditorWindow(screen:NSScreen.main ?? NSScreen.screens[0],config:source,audio:SystemAudio(),media:NowPlaying());precondition(actualEditor.contentView!.subviews.count==3);var editorFinished=false;actualEditor.onFinish={editorFinished=true};actualEditor.finish(true);precondition(editorFinished,"Closing editor failed to resume wallpapers")
 let orbConfig=source.preview();orbConfig.scene=SceneDocument(items:[SceneItem(kind:"orb",x:0.3,y:0.2,width:0.3,height:0.5)]);let orbRoot=SceneRoot(frame:CGRect(origin:.zero,size:size),config:orbConfig,audio:SystemAudio(),media:NowPlaying());let orb=orbRoot.subviews.first as! SceneNode;precondition(orb.metal!.frame.width>orb.bounds.width && abs(orb.proxy.n("radius")/100*orb.metal!.frame.height-150)<1,"Orb deformation clipped or nominal radius changed")
 orbConfig.scene!.items[0].x=0.9;orbConfig.scene!.items[0].rotation=90;orbRoot.refresh();precondition(orb.metal!.frame.width>orb.metal!.frame.height,"Offscreen rotated orb clipped before rotation")
 let retainedMetal=orb.metal!,retainedRenderer=orb.renderer!
 for i in 0..<40{orbConfig.scene!.items[0].style["wirecolor"] = .string(i%2==0 ? "#00ff00":"#ff143c");orbConfig.scene!.items[0].style["density"] = .number(Double(24+i));orbConfig.scene!.items[0].opacity=0.5;orbRoot.refresh();precondition(orb.metal === retainedMetal && orb.renderer === retainedRenderer,"Live properties recreated Metal renderer")}
 let layers=SceneEditorModel(source,size);layers.document.items=[SceneItem(id:"back",kind:"text"),SceneItem(id:"middle",kind:"text"),SceneItem(id:"front",kind:"text")];layers.reorder("back","middle",true);precondition(layers.document.items.map(\.id)==["middle","back","front"]);layers.history(true);precondition(layers.document.items.map(\.id)==["back","middle","front"])
 if let i=CommandLine.arguments.firstIndex(of:"--gif-fixture"),CommandLine.arguments.count>i+1{let path=CommandLine.arguments[i+1];let codec=CGImageSourceCreateWithURL(URL(fileURLWithPath:path) as CFURL,nil)!;precondition(CGImageSourceGetCount(codec)==3);let image=NSImage(contentsOfFile:path)!;let view=NSImageView();view.image=image;view.animates=true;view.imageScaling = .scaleAxesIndependently;precondition(view.animates && view.image != nil)}
 print("PASS native editor draft isolation, duplicate/undo/redo/save, mouse drag/resize, snapping, canvas hit testing and retained scene nodes");exit(0)
}
if CommandLine.arguments.contains("--scene-self-test"){
 let stars=StarMotion(),a=stars.position(0,width:1920,height:1080),b=stars.position(1,width:1920,height:1080);for _ in 0..<60{stars.advance(count:120,dt:1/60,speed:20,direction:135,paused:false)};let aa=stars.position(0,width:1920,height:1080),bb=stars.position(1,width:1920,height:1080);precondition(abs((aa.x-a.x)-(bb.x-b.x))>1);stars.advance(count:120,dt:0.1,speed:200,direction:0,paused:true);precondition(stars.position(0,width:1920,height:1080).x==aa.x)
 precondition(CiderReader.tempo(["bpm":128.5])==128.5 && CiderReader.tempo(["attributes":["tempo":"128.5"]])==128.5 && CiderReader.tempo(["bpm":true])==0 && CiderReader.tempo(["bpm":9999])==0 && CiderReader.tempo([:])==0)
 let player=NowPlaying();player.provider="Cider";player.playing=true;player.bpm=128.5;precondition(player.tempoBpm==128.5);player.playing=false;precondition(player.tempoBpm==0);player.playing=true;player.provider="Spotify";precondition(player.tempoBpm==0)
 print("PASS independent stars, pause and optional Cider BPM with audio fallback")

 for expected in [40.0,60.0,90.0,120.0,165.0,190.0,220.0,240.0]{let detector=BeatTracker();var result=BeatFrame();let period=60/expected,ranges=[ClosedRange<Double>](repeating:0...200,count:64)
  for i in 0..<900{let phase=(Double(i)/60).truncatingRemainder(dividingBy:period),energy=phase<0.09 ? exp(-phase*30)*0.7:0;result=detector.feed(bands:[Float](repeating:Float(energy),count:64),ranges:ranges,energy:energy,dt:1/60)}
  precondition(abs(result.bpm-expected)<8,"Tempo estimate differs from click train");print("PASS tempo \(expected) BPM estimated as \(result.bpm)")
  for _ in 0..<180{result=detector.feed(bands:[Float](repeating:0,count:64),ranges:ranges,energy:0,dt:1/60)};precondition(result.bpm==0 && result.pulse==0)
 }
 for expected in [90.0,120.0,165.0]{let analyzer=SpectrumAnalyzer(),rate=48000,count=48000*12;var pcm=[Float](repeating:0,count:count)
  for i in 0..<count{let t=Double(i)/Double(rate),phase=t.truncatingRemainder(dividingBy:60/expected),envelope=0.7*exp(-phase*45),oscillator=sin(2*Double.pi*65*t);pcm[i]=Float(envelope*oscillator)}
  pcm.withUnsafeBufferPointer{analyzer.ingest($0.baseAddress!,frames:count,channels:1,rate:Double(rate))};let bpm=analyzer.snapshot().bpm;precondition(abs(bpm-expected)<8,"PCM tempo estimate differs");print("PASS PCM kick audio \(expected) BPM estimated as \(bpm)")
 }
 var motion=BackgroundMotion();for _ in 0..<60{motion.advance(dt:1/60,base:20,direction:135,boost:3,beat:0,bpm:0,paused:false)};precondition(abs(motion.x+sqrt(200))<0.000001 && abs(motion.y-sqrt(200))<0.000001)
 let date=Date(timeIntervalSince1970:13*3600+5*60),oldZone=NSTimeZone.default;NSTimeZone.default=TimeZone(secondsFromGMT:0)!;precondition(ClockOverlay.text(date,hours24:true)=="13:05" && ClockOverlay.text(date,hours24:false)=="01:05 PM");NSTimeZone.default=oldZone
 print("PASS clock 12/24-hour formatting and silent constant diagonal movement with beat decay");exit(0)
}
if CommandLine.arguments.contains("--startup-self-test"){
 let name="WirePulseStartupTest-"+UUID().uuidString,store=UserDefaults(suiteName:name)!
 var state=SMAppService.Status.notRegistered,registrations=0,removals=0
 let manager=StartupManager(defaults:store,read:{state},register:{registrations+=1;state = .enabled},unregister:{removals+=1;state = .notRegistered})
 precondition(StartupManager.shouldPrompt(asked:false,autostart:false) && !StartupManager.shouldPrompt(asked:true,autostart:false) && !StartupManager.shouldPrompt(asked:false,autostart:true))
 manager.setEnabled(false);precondition(store.bool(forKey:"startupPrompted") && registrations==0 && !manager.enabled)
 manager.setEnabled(true);precondition(manager.enabled && registrations==1)
 manager.setEnabled(true);precondition(registrations==1)
 manager.setEnabled(false);precondition(!manager.enabled && removals==1)
 store.removePersistentDomain(forName:name)
 print("PASS startup consent, enable/disable and no duplicate registration; no real login item modified");exit(0)
}
if let index=CommandLine.arguments.firstIndex(of:"--relocation-probe"),CommandLine.arguments.count>index+1{
 do{
  let old=Bundle.main.executableURL!,destination=URL(fileURLWithPath:CommandLine.arguments[index+1])
  try FileManager.default.moveItem(at:Bundle.main.bundleURL,to:destination)
  guard let actual=currentExecutableURL(),actual.path==destination.appendingPathComponent("Contents/MacOS/WirePulse").path,actual.path != old.path else{fatalError("Relocated executable was not resolved")}
  let child=Process();child.executableURL=actual;child.arguments=["--self-test"];try child.run();child.waitUntilExit();guard child.terminationStatus==0 else{fatalError("Relocated helper launch failed")}
  print("PASS: running bundle moved; cached path changed; child launched from the new location");exit(0)
 }catch{fputs("Relocation test failed: \(error)\n",stderr);exit(1)}
}
if CommandLine.arguments.contains("--provider-diagnostic"){
 let bundles=ProviderDetection.activeOutputBundles();let selected=ProviderDetection.browser(from:bundles)
 print("Active output processes: \(bundles.count); browser/application fallback: \(selected?.name ?? "none")");exit(0)
}
if CommandLine.arguments.contains("--music-artwork"){
 let index=CommandLine.arguments.firstIndex(of:"--music-artwork")!;let pid=CommandLine.arguments.count>index+1 ? CommandLine.arguments[index+1]:"";exit(exportMusicArtwork(expectedID:pid))
}
if let i=CommandLine.arguments.firstIndex(of:"--cider-fixture-test"),CommandLine.arguments.count>i+1{
 let expectedIndex=CommandLine.arguments.firstIndex(of:"--expected-bpm"),expected=expectedIndex.flatMap{Double(CommandLine.arguments[$0+1])} ?? 0
 guard let info=CiderReader.read(address:CommandLine.arguments[i+1],token:"fixture-token"),info["provider"] as? String=="Cider",info["title"] as? String=="WirePulse fixture",info["position"] as? Double==12,info["bpm"] as? Double==expected else{fputs("Cider fixture failed\n",stderr);exit(1)}
 print("PASS Cider API metadata/state/time/artwork");exit(0)
}
if CommandLine.arguments.contains("--display-profile-test"){
 let key="test-"+UUID().uuidString,root=Configuration(),before=root.n("sphereX"),a=Configuration(profile:key,template:root)
 precondition(a.n("sphereX")==before);a.values["sphereX"]=23;a.values["bgstyle"]=1;a.values["clock24"]=0;a.clockFont="Menlo";let b=Configuration(profile:key,template:root);precondition(b.n("sphereX")==23 && root.n("sphereX")==before && b.n("bgstyle")==1 && b.n("clock24")==0 && b.clockFont=="Menlo")
 for field in ["values","colors","backgroundPath","topCentered","topMargin","clockFont"]{UserDefaults.standard.removeObject(forKey:"display-"+key+"-"+field)}
 print("PASS independent display profile persists without changing shared layout");exit(0)
}
if CommandLine.arguments.contains("--self-test"){
 for rate in [44100.0,48000.0,96000.0,192000.0]{
  for frequency in [20.0,1000.0,rate*0.47]{
   let a=SpectrumAnalyzer();a.configure(rate:rate);let n=a.snapshot().fftSize
   let signal=(0..<(n*2)).map{Float(sin(2*Double.pi*frequency*Double($0)/rate)*0.4)}
   signal.withUnsafeBufferPointer{a.ingest($0.baseAddress!,frames:$0.count,channels:1,rate:rate)}
   let s=a.snapshot();precondition(abs(s.peakFrequencyHz-frequency)<=s.binWidthHz)
   precondition(s.maximumHz==rate/2 && s.peak>0.1 && s.ranges.count==64)
   precondition(s.ranges.first!.lowerBound==0 && s.ranges.last!.upperBound==rate/2)
   for i in 1..<64{precondition(abs(s.ranges[i-1].upperBound-s.ranges[i].lowerBound)<0.000001)}
   print("PASS \(Int(rate)) Hz: tone \(frequency) → \(s.peakFrequencyHz) Hz; full range 0–\(s.maximumHz), FFT \(s.fftSize)")
  }
 }
 let a=SpectrumAnalyzer(),rate=48000.0,n=32768
 var stereo=[Float](repeating:0,count:n*2)
 for i in 0..<n{let x=Float(sin(2*Double.pi*1000*Double(i)/rate)*0.4);stereo[i*2]=x;stereo[i*2+1] = -x}
 stereo.withUnsafeBufferPointer{a.ingest($0.baseAddress!,frames:n,channels:2,rate:rate)}
 let opposite=a.snapshot();precondition(opposite.peak>0.5 && abs(opposite.peakFrequencyHz-1000)<=opposite.binWidthHz)
 stereo=[Float](repeating:0,count:n*2);stereo.withUnsafeBufferPointer{a.ingest($0.baseAddress!,frames:n,channels:2,rate:rate)};precondition(a.snapshot().bands.allSatisfy{$0==0})
 let dc=[Float](repeating:0.1,count:n);dc.withUnsafeBufferPointer{a.ingest($0.baseAddress!,frames:n,channels:1,rate:rate)};precondition(a.snapshot().bands[0]>0.5)
 let nyquist=(0..<n).map{Float($0%2==0 ? 0.4:-0.4)};nyquist.withUnsafeBufferPointer{a.ingest($0.baseAddress!,frames:n,channels:1,rate:rate)}
 precondition(abs(a.snapshot().peakFrequencyHz-rate/2)<=a.snapshot().binWidthHz && a.snapshot().bands[63]>0)
 print("PASS: opposite-phase stereo retains energy; silence is zero; DC and Nyquist endpoints are covered")
 exit(0)
}
if let index=CommandLine.arguments.firstIndex(of:"--render-preview"),CommandLine.arguments.count>index+1{
 let _=NSApplication.shared;let config=Configuration(),analyzer=SpectrumAnalyzer()
 var signal=[Float](repeating:0,count:8192)
 for i in signal.indices {
  let phase:Double=Double(i)*(2*Double.pi/48000)
  let bass:Double=0.3*sin(110*phase)
  let mid:Double=0.15*sin(1000*phase)
  let treble:Double=0.08*sin(6000*phase)
  signal[i]=Float(bass+mid+treble)
 }
 signal.withUnsafeBufferPointer{analyzer.ingest($0.baseAddress!,frames:$0.count,channels:1,rate:48000)}
 do{let r=try WallpaperRenderer(config:config,analyzer:analyzer,device:MTLCreateSystemDefaultDevice()!);signal.withUnsafeBufferPointer{analyzer.ingest($0.baseAddress!,frames:$0.count,channels:1,rate:48000)};try r.renderPreview(to:URL(fileURLWithPath:CommandLine.arguments[index+1]));print("Metal preview rendered from a three-tone PCM signal");exit(0)}catch{fputs("\(error)\n",stderr);exit(1)}
}
let app=NSApplication.shared,delegate=AppDelegate();app.delegate=delegate;app.run()
