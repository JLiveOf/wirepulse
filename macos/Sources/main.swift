import AppKit
import MetalKit
import ServiceManagement

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
