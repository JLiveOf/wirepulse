import AVFoundation

// Both buffers are scheduled on one node. The sustain buffer loops sample for
// sample without reopening the output device or repeating the introduction.
final class SetupMusic {
 private let engine=AVAudioEngine(),node=AVAudioPlayerNode()
 private var buffers=[AVAudioPCMBuffer](),started=false
 var muted=false{didSet{node.volume=muted ? 0:1}}
 func start(){guard !started else{return};started=true
  do{
   let intro=try Self.load("Setup-intro"),loop=try Self.load("Setup-loop")
   guard intro.format.sampleRate==loop.format.sampleRate,intro.format.channelCount==loop.format.channelCount else{throw NSError(domain:"WirePulse",code:1,userInfo:[NSLocalizedDescriptionKey:"Welcome music formats differ"])}
   buffers=[intro,loop];engine.attach(node);engine.connect(node,to:engine.mainMixerNode,format:intro.format);engine.prepare();try engine.start();Self.enqueue(node,intro,loop);node.volume=muted ? 0:1;node.play()
  }catch{NSLog("Setup music: %@",error.localizedDescription);stop()}
 }
 private static func enqueue(_ node:AVAudioPlayerNode,_ intro:AVAudioPCMBuffer,_ loop:AVAudioPCMBuffer){node.scheduleBuffer(intro,at:nil,options:[],completionHandler:nil);node.scheduleBuffer(loop,at:nil,options:.loops,completionHandler:nil)}
 static func verifyOffline()throws{
  let intro=try load("Setup-intro"),loop=try load("Setup-loop"),engine=AVAudioEngine(),node=AVAudioPlayerNode()
  precondition(intro.frameLength==1_536_000 && loop.frameLength==1_536_000 && intro.format.sampleRate==48000 && loop.format.sampleRate==48000)
  engine.attach(node);engine.connect(node,to:engine.mainMixerNode,format:intro.format);try engine.enableManualRenderingMode(.offline,format:intro.format,maximumFrameCount:4096);try engine.start();enqueue(node,intro,loop);node.play();defer{node.stop();engine.stop()}
  let output=AVAudioPCMBuffer(pcmFormat:engine.manualRenderingFormat,frameCapacity:4096)!,limit=Int(intro.frameLength)+Int(loop.frameLength)*2+1024
  var frames=0,retries=0
  while frames<limit{let count=AVAudioFrameCount(min(4096,limit-frames));let status=try engine.renderOffline(count,to:output);guard status == .success else{retries+=1;if retries>100{throw NSError(domain:"WirePulse music",code:1)};continue};let length=Int(output.frameLength);guard length>0 else{throw NSError(domain:"WirePulse music",code:2)}
   for offset in stride(from:0,to:length,by:97){let index=frames+offset,source=index<Int(intro.frameLength) ? intro:loop,position=index<Int(intro.frameLength) ? index:(index-Int(intro.frameLength))%Int(loop.frameLength);for channel in 0..<Int(source.format.channelCount){let expected=source.floatChannelData![channel][position],actual=output.floatChannelData![channel][offset];if abs(expected-actual)>0.0001{throw NSError(domain:"WirePulse music",code:3,userInfo:[NSLocalizedDescriptionKey:"Offline intro/loop mismatch at frame \(index)"])}}};frames+=length
  }
  print("PASS native offline music: introduction once, seamless sustain loop and repeated loop boundary")
 }
 static func load(_ name:String)throws->AVAudioPCMBuffer{guard let url=Bundle.main.url(forResource:name,withExtension:"wav") else{throw NSError(domain:"WirePulse",code:1)};let file=try AVAudioFile(forReading:url);guard file.length>0,file.length<=20_000_000,let buffer=AVAudioPCMBuffer(pcmFormat:file.processingFormat,frameCapacity:AVAudioFrameCount(file.length)) else{throw NSError(domain:"WirePulse",code:2)};try file.read(into:buffer);return buffer}
 func stop(){node.stop();engine.stop();buffers=[]}
 deinit{stop()}
}
struct WhatsNewContent:Decodable {
 struct Feature:Decodable{let icon:String,title:String,description:String}
 let version:String,features:[Feature]
 static let current:WhatsNewContent={if let url=Bundle.main.url(forResource:"whats-new",withExtension:"json"),let data=try? Data(contentsOf:url),let notes=try? JSONDecoder().decode(WhatsNewContent.self,from:data),!notes.features.isEmpty{return notes};return WhatsNewContent(version:UpdateManager.buildVersion,features:[Feature(icon:"sparkles",title:"Novedades de WirePulse",description:"Consulta las notas de esta versión en GitHub.")])}()
}
