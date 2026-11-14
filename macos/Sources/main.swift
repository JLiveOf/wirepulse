import AppKit
import MetalKit

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
 guard let info=CiderReader.read(address:CommandLine.arguments[i+1],token:"fixture-token"),info["provider"] as? String=="Cider",info["title"] as? String=="WirePulse fixture",info["position"] as? Double==12 else{fputs("Cider fixture failed\n",stderr);exit(1)}
 print("PASS Cider API metadata/state/time/artwork");exit(0)
}
if CommandLine.arguments.contains("--display-profile-test"){
 let key="test-"+UUID().uuidString,root=Configuration(),before=root.n("sphereX"),a=Configuration(profile:key,template:root)
 precondition(a.n("sphereX")==before);a.values["sphereX"]=23;let b=Configuration(profile:key,template:root);precondition(b.n("sphereX")==23 && root.n("sphereX")==before)
 for field in ["values","colors","backgroundPath","topCentered","topMargin"]{UserDefaults.standard.removeObject(forKey:"display-"+key+"-"+field)}
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
 let signal=(0..<8192).map{i->Float in let t=Double(i)/48000;return Float(0.3*sin(2*Double.pi*110*t)+0.15*sin(2*Double.pi*1000*t)+0.08*sin(2*Double.pi*6000*t))}
 signal.withUnsafeBufferPointer{analyzer.ingest($0.baseAddress!,frames:$0.count,channels:1,rate:48000)}
 do{let r=try WallpaperRenderer(config:config,analyzer:analyzer,device:MTLCreateSystemDefaultDevice()!);signal.withUnsafeBufferPointer{analyzer.ingest($0.baseAddress!,frames:$0.count,channels:1,rate:48000)};try r.renderPreview(to:URL(fileURLWithPath:CommandLine.arguments[index+1]));print("Metal preview rendered from a three-tone PCM signal");exit(0)}catch{fputs("\(error)\n",stderr);exit(1)}
}
let app=NSApplication.shared,delegate=AppDelegate();app.delegate=delegate;app.run()
