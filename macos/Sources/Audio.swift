import Foundation
import CoreAudio
import Accelerate

struct SpectrumSnapshot {
 let bands:[Float],age:Double,frames:UInt64,peak:Float
 let sampleRate:Double,fftSize:Int,binWidthHz:Double,peakFrequencyHz:Double
 let beat:Double,bpm:Double,beatConfidence:Double
 let ranges:[ClosedRange<Double>]
 var maximumHz:Double {sampleRate/2}
}
final class SpectrumAnalyzer {
 private var beatTracker=BeatTracker(),publishedBeat=BeatFrame()
 private var fft:vDSP_DFT_Setup?
 private var count=0,rate=0.0,cursor=0,hop=0,hopSize=800
 private var left=[Float](),right=[Float](),window=[Float](),real=[Float](),imag=[Float](),outReal=[Float](),outImag=[Float](),power=[Float]()
 private var starts=[Int](),ends=[Int](),ranges=[ClosedRange<Double>](),windowSum:Float=1
 private let lock=NSLock()
 private var published=[Float](repeating:0,count:64),lastSample=0.0,frames:UInt64=0,peak:Float=0,peakHz=0.0
 private var publishedRate=0.0,publishedSize=0,publishedRanges=[ClosedRange<Double>]()
 init(){configure(rate:48000)}
 deinit{if let fft=fft{vDSP_DFT_DestroySetup(fft)}}
 // Called while capture is stopped, or on the sole input queue before consuming a new format.
 func configure(rate:Double){
  guard rate.isFinite,rate>0,rate != self.rate else{return}
  if let fft=fft{vDSP_DFT_DestroySetup(fft)}
  beatTracker=BeatTracker();self.rate=rate;count=8192;while Double(count)<rate/6 && count<65536{count *= 2}
  fft=vDSP_DFT_zop_CreateSetup(nil,vDSP_Length(count),.FORWARD)!;cursor=0;hop=0;hopSize=max(1,Int(rate/60))
  left=[Float](repeating:0,count:count);right=left;window=left;real=left;imag=left;outReal=left;outImag=left;power=[Float](repeating:0,count:count/2+1)
  vDSP_hann_window(&window,vDSP_Length(count),Int32(vDSP_HANN_NORM));windowSum=window.reduce(0,+)
  // DC is its own display band. Every positive-frequency FFT bin, including Nyquist,
  // belongs to exactly one of the remaining logarithmic bands; no gaps or duplicated bins.
  starts=[0];ends=[1];ranges=[0...(rate/Double(count)*0.5)]
  let half=count/2;var boundary=1
  for b in 0..<63{
   let end=b==62 ? half+1 : max(boundary+1,min(half+1-(62-b),Int(pow(Double(half+1),Double(b+1)/63).rounded())))
   starts.append(boundary);ends.append(end)
   ranges.append((Double(boundary)-0.5)*rate/Double(count)...min(rate/2,(Double(end)-0.5)*rate/Double(count)));boundary=end
  }
  lock.lock();publishedBeat=BeatFrame();published=[Float](repeating:0,count:64);publishedRate=rate;publishedSize=count;publishedRanges=ranges;peak=0;peakHz=0;lastSample=0;lock.unlock()
 }
 func bandIndex(for frequency:Double)->Int {ranges.firstIndex{$0.contains(frequency)} ?? 63}
 private func append(_ l:Float,_ r:Float){
  left[cursor]=l.isFinite ? l:0;right[cursor]=r.isFinite ? r:0;cursor=(cursor+1)%count;hop+=1
  if hop>=hopSize{hop=0;analyze()}
 }
 func ingest(_ samples:UnsafePointer<Float>,frames:Int,channels:Int,rate:Double){
  guard channels>0,frames>0 else{return};configure(rate:rate)
  for f in 0..<frames{append(samples[f*channels],samples[f*channels+(channels>1 ? 1:0)])}
 }
 func ingestPlanar(_ buffers:UnsafeMutableAudioBufferListPointer,rate:Double){
  guard let first=buffers.first,let data=first.mData else{return}
  let channels=max(1,Int(first.mNumberChannels)),frames=Int(first.mDataByteSize)/4/channels;guard frames>0 else{return}
  if buffers.count==1{ingest(data.assumingMemoryBound(to:Float.self),frames:frames,channels:channels,rate:rate);return}
  configure(rate:rate);let second=buffers[1],l=data.assumingMemoryBound(to:Float.self)
  guard let rData=second.mData else{return};let r=rData.assumingMemoryBound(to:Float.self),rc=max(1,Int(second.mNumberChannels)),available=min(frames,Int(second.mDataByteSize)/4/rc)
  for f in 0..<available{append(l[f*channels],r[f*rc])}
 }
 private func analyze(){
  guard let fft=fft else{return};let half=count/2
  for k in 0...half{power[k]=0}
  for channel in 0..<2{
   for i in 0..<count{real[i]=(channel==0 ? left[(cursor+i)%count]:right[(cursor+i)%count])*window[i]}
   vDSP_DFT_Execute(fft,real,imag,&outReal,&outImag)
   for k in 0...half{let scale:Float=(k==0 || k==half ? 1:2)/windowSum;power[k] += (outReal[k]*outReal[k]+outImag[k]*outImag[k])*scale*scale*0.5}
  }
  var bins=[Float](repeating:0,count:64)
  for band in 0..<64{var sum:Float=0;for k in starts[band]..<ends[band]{sum += power[k]};let magnitude=sqrt(sum/Float(ends[band]-starts[band]));bins[band]=max(0,min(1,(20*log10(max(0.0000001,magnitude))+72)/60))}
  var maximum:Float=0;var index=0;for k in 0...half{if power[k]>maximum{maximum=power[k];index=k}}
  let beat=beatTracker.feed(bands:bins,ranges:ranges,energy:sqrt(power.reduce(0.0){$0+Double($1)}),dt:Double(hopSize)/rate)
  lock.lock();publishedBeat=beat;published=bins;lastSample=ProcessInfo.processInfo.systemUptime;frames+=1;peak=bins.max() ?? 0;peakHz=Double(index)*rate/Double(count);lock.unlock()
 }
 func snapshot()->SpectrumSnapshot{lock.lock();defer{lock.unlock()};return SpectrumSnapshot(bands:published,age:ProcessInfo.processInfo.systemUptime-lastSample,frames:frames,peak:peak,sampleRate:publishedRate,fftSize:publishedSize,binWidthHz:publishedRate/Double(max(1,publishedSize)),peakFrequencyHz:peakHz,beat:publishedBeat.pulse,bpm:publishedBeat.bpm,beatConfidence:publishedBeat.confidence,ranges:publishedRanges)}
}
final class SystemAudio: ObservableObject {
 @Published var status="Audio sin activar"
 @Published var running=false
 @Published var starting=false
 let analyzer=SpectrumAnalyzer()
 private var tap:AudioObjectID=0,aggregate:AudioObjectID=0,proc:AudioDeviceIOProcID?
 private var formatListener:AudioObjectPropertyListenerBlock?
 private let queue=DispatchQueue(label:"dev.wirepulse.audio",qos:.userInitiated)
 private let control=DispatchQueue(label:"dev.wirepulse.audio-control",qos:.userInitiated)
 private let stateLock=NSLock();private var generation=0,desired=false
 private func request(_ enabled:Bool)->Int{stateLock.lock();defer{stateLock.unlock()};generation+=1;desired=enabled;return generation}
 private func current(_ token:Int)->Bool{stateLock.lock();defer{stateLock.unlock()};return generation==token && desired}
 var requested:Bool{stateLock.lock();defer{stateLock.unlock()};return desired}
 private func matches(_ token:Int)->Bool{stateLock.lock();defer{stateLock.unlock()};return generation==token}
 private func publish(_ token:Int,_ active:Bool,_ message:String,pending:Bool=false){DispatchQueue.main.async{[weak self] in guard let self=self,self.current(token) else{return};self.running=active;self.starting=pending;self.status=message}}
 func start(){let token=request(true);publish(token,false,"Iniciando captura · permite el audio del sistema si macOS lo solicita…",pending:true);control.async{[weak self] in guard let self=self,self.current(token) else{return};self.startDevice(token)}}
 private func startDevice(_ token:Int){
  stopDevice()
  do{
   let description=CATapDescription(stereoGlobalTapButExcludeProcesses:[]);description.name="WirePulse · audio del sistema";description.uuid=UUID();description.isPrivate=true;description.muteBehavior = .unmuted
   try check(AudioHardwareCreateProcessTap(description,&tap),"Crear captura de audio")
   var format=AudioStreamBasicDescription();var size=UInt32(MemoryLayout<AudioStreamBasicDescription>.size);var address=AudioObjectPropertyAddress(mSelector:kAudioTapPropertyFormat,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
   try check(AudioObjectGetPropertyData(tap,&address,0,nil,&size,&format),"Leer formato")
   guard format.mFormatFlags & kAudioFormatFlagIsFloat != 0,format.mBitsPerChannel==32 else{throw NSError(domain:"WirePulse",code:1,userInfo:[NSLocalizedDescriptionKey:"El dispositivo no proporciona audio Float32 compatible."])}
   let descriptor:[String:Any]=[kAudioAggregateDeviceNameKey:"WirePulse Audio (privado)",kAudioAggregateDeviceUIDKey:"dev.wirepulse.tap.\(UUID().uuidString)",kAudioAggregateDeviceIsPrivateKey:true,kAudioAggregateDeviceTapAutoStartKey:true,kAudioAggregateDeviceTapListKey:[[kAudioSubTapUIDKey:description.uuid.uuidString,kAudioSubTapDriftCompensationKey:true]]]
   try check(AudioHardwareCreateAggregateDevice(descriptor as CFDictionary,&aggregate),"Crear dispositivo privado")
   let rate=format.mSampleRate
   analyzer.configure(rate:rate)
   try check(AudioDeviceCreateIOProcIDWithBlock(&proc,aggregate,queue){[weak self] _,input,_,_,_ in
    self?.analyzer.ingestPlanar(UnsafeMutableAudioBufferListPointer(UnsafeMutablePointer(mutating:input)),rate:rate)
   },"Conectar analizador")
   try check(AudioDeviceStart(aggregate,proc),"Iniciar captura")
   guard current(token) else{stopDevice();return}
   publish(token,true,"PCM Float32 · \(Int(rate)) Hz · análisis 0–\(Int(rate/2)) Hz")
   let observedTap=tap
   let listener:AudioObjectPropertyListenerBlock={ [weak self] _,_ in
    self?.control.async{[weak self] in guard let self=self,self.current(token),self.tap==observedTap else{return};self.start()}
   }
   formatListener=listener
   AudioObjectAddPropertyListenerBlock(tap,&address,.main,listener)
  }catch{stopDevice();publish(token,false,error.localizedDescription+". Revisa Privacidad y seguridad → Grabación de audio del sistema.")}
 }
 func stop(){let token=request(false);DispatchQueue.main.async{[weak self] in guard let self=self,self.matches(token) else{return};self.running=false;self.starting=false;self.status="Audio detenido"};control.async{[weak self] in self?.stopDevice()}}
 private func stopDevice(){if let listener=formatListener,tap != 0{var address=AudioObjectPropertyAddress(mSelector:kAudioTapPropertyFormat,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain);AudioObjectRemovePropertyListenerBlock(tap,&address,.main,listener)};formatListener=nil;if let proc=proc,aggregate != 0{AudioDeviceStop(aggregate,proc);AudioDeviceDestroyIOProcID(aggregate,proc)};proc=nil;if aggregate != 0{AudioHardwareDestroyAggregateDevice(aggregate);aggregate=0};if tap != 0{AudioHardwareDestroyProcessTap(tap);tap=0}}
 private func check(_ code:OSStatus,_ action:String)throws{if code != noErr{throw NSError(domain:"WirePulse",code:Int(code),userInfo:[NSLocalizedDescriptionKey:"\(action): error \(code)"])}}
}
