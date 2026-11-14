import Foundation

struct BeatFrame {var pulse=0.0,bpm=0.0,confidence=0.0;var count:Int64=0}
final class BeatTracker {
 private var previous=[Double](repeating:0,count:64),history=[Double](repeating:0,count:480),intervals=[Double](repeating:0,count:12)
 private var cursor=0,samples=0,intervalCount=0,intervalCursor=0,estimateAt=0
 private var time=0.0,lastBeat = -100.0,mean=0.001,pulse=0.0,bpm=0.0,confidence=0.0,quiet=0.0,previousEnergy=0.0
 private var beats:Int64=0
 func feed(bands:[Float],ranges:[ClosedRange<Double>],energy:Double,dt:Double)->BeatFrame{
  time+=dt;pulse*=exp(-dt*7);var novelty=0.0,weight=0.0
  for i in 0..<64{let w=ranges.indices.contains(i) && ranges[i].upperBound<=250 ? 2.0:1.0;novelty+=max(0,Double(bands[i])-previous[i])*w;weight+=w;previous[i]=Double(bands[i])}
  novelty/=max(1,weight);novelty=max(novelty,max(0,energy-previousEnergy)*0.25);previousEnergy=energy
  if energy<0.0001{quiet+=dt;novelty=0}else{quiet=0}
  let threshold=max(0.002,mean*1.8)
  if quiet==0 && novelty>threshold && time-lastBeat>0.18{let interval=time-lastBeat;if interval>0.23 && interval<1.6{intervals[intervalCursor%12]=interval;intervalCursor+=1;intervalCount=min(intervalCount+1,12)};lastBeat=time;beats+=1;pulse=min(1,max(0.3,novelty/max(0.008,threshold*2)))}
  mean+=min(1,dt/1.5)*(novelty-mean);history[cursor]=novelty;cursor=(cursor+1)%480;samples=min(480,samples+1);estimateAt+=1
  if estimateAt>=30{estimateAt=0;estimate(dt:dt)}
  if quiet>2{bpm=0;confidence=0;pulse=0}
  return BeatFrame(pulse:pulse,bpm:bpm,confidence:confidence,count:beats)
 }
 private func estimate(dt:Double){guard samples>=180,intervalCount>=2 else{return};let sorted=Array(intervals.prefix(intervalCount)).sorted();var prior=60/sorted[sorted.count/2];while prior<40{prior*=2};while prior>240{prior/=2}
  var best=0.0,bestLag=0;let minimum=max(2,Int(floor(60/(240*dt)))),maximum=min(samples/2,Int(ceil(60/(40*dt))))
  for lag in minimum...maximum{var dot=0.0,a=0.0,b=0.0;for i in lag..<samples{let x=history[(cursor-1-i+960)%480],y=history[(cursor-1-i+lag+960)%480];dot+=x*y;a+=x*x;b+=y*y};let correlation=dot/sqrt(max(1e-12,a*b)),candidate=60/(Double(lag)*dt),ratio=log(candidate/prior)/0.15,score=correlation+0.6*exp(-ratio*ratio);if score>best{best=score;bestLag=lag;confidence=correlation}}
  if bestLag>0 && confidence>0.18{let candidate=60/(Double(bestLag)*dt);bpm=bpm==0 || abs(bpm-candidate)>15 ? candidate:bpm*0.7+candidate*0.3}
 }
}
struct BackgroundMotion {
 var x=0.0,y=0.0,speed=0.0
 mutating func advance(dt:Double,base:Double,direction:Double,boost:Double,beat:Double,bpm:Double,paused:Bool){guard !paused else{return};let delta=min(0.1,max(0,dt));speed=base*(1+boost*min(1,max(0,beat))*(bpm>0 ? min(2,max(0.5,bpm/120)):1));let angle=direction*Double.pi/180;x+=cos(angle)*speed*delta;y+=sin(angle)*speed*delta}
}
