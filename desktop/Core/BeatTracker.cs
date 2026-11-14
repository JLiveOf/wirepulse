namespace WirePulse;
public readonly record struct BeatFrame(double Pulse,double Bpm,double Confidence,long Count);

/// Spectral-flux onset detector plus autocorrelation tempo estimate. No song metadata or fake beat clock.
public sealed class BeatTracker {
 readonly double[] previous=new double[64],history=new double[480],intervals=new double[12];int cursor,samples,intervalCount,intervalCursor,estimateAt;double time,lastBeat=-100,mean=.001,pulse,bpm,confidence,quiet,previousEnergy;long beats;
 public BeatFrame Feed(ReadOnlySpan<double> bands,ReadOnlySpan<double> high,double energy,double dt){time+=dt;pulse*=Math.Exp(-dt*7);double novelty=0,weight=0;for(int i=0;i<64;i++){double w=high[i]<=250?2:1;novelty+=Math.Max(0,bands[i]-previous[i])*w;weight+=w;previous[i]=bands[i];}novelty/=weight;novelty=Math.Max(novelty,Math.Max(0,energy-previousEnergy)*.25);previousEnergy=energy;
  if(energy<.0001){quiet+=dt;novelty=0;}else quiet=0;
  double threshold=Math.Max(.002,mean*1.8);if(quiet==0&&novelty>threshold&&time-lastBeat>.18){double interval=time-lastBeat;if(interval is >.23 and <1.6){intervals[intervalCursor++%intervals.Length]=interval;intervalCount=Math.Min(intervalCount+1,intervals.Length);}lastBeat=time;beats++;pulse=Math.Clamp(novelty/Math.Max(.008,threshold*2),.3,1);}
  mean+=Math.Min(1,dt/1.5)*(novelty-mean);history[cursor]=novelty;cursor=(cursor+1)%history.Length;samples=Math.Min(history.Length,samples+1);
  if(++estimateAt>=30){estimateAt=0;Estimate(dt);}if(quiet>2){bpm=confidence=pulse=0;}
  return new(pulse,bpm,confidence,beats);
 }
 void Estimate(double dt){if(samples<180||intervalCount<2)return;var sorted=new double[intervalCount];Array.Copy(intervals,sorted,intervalCount);Array.Sort(sorted);double prior=60/sorted[sorted.Length/2];while(prior<40)prior*=2;while(prior>240)prior/=2;
  double best=0;int bestLag=0;int minLag=Math.Max(2,(int)Math.Floor(60/(240*dt))),maxLag=Math.Min(samples/2,(int)Math.Ceiling(60/(40*dt)));
  for(int lag=minLag;lag<=maxLag;lag++){double dot=0,a=0,b=0;for(int i=lag;i<samples;i++){double x=history[(cursor-1-i+history.Length*2)%history.Length],y=history[(cursor-1-i+lag+history.Length*2)%history.Length];dot+=x*y;a+=x*x;b+=y*y;}double correlation=dot/Math.Sqrt(Math.Max(1e-12,a*b));double candidate=60/(lag*dt);double score=correlation+.6*Math.Exp(-Math.Pow(Math.Log(candidate/prior)/.15,2));if(score>best){best=score;bestLag=lag;confidence=correlation;}}
  if(bestLag>0&&confidence>.18){double candidate=60/(bestLag*dt);bpm=bpm==0||Math.Abs(bpm-candidate)>15?candidate:bpm*.7+candidate*.3;}
 }
}
