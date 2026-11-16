namespace WirePulse;

// Safety ceiling for extra compositor invalidations. Normal cadence remains fractional.
public sealed class FrameBudget {
 readonly Queue<double> submitted=[];
 public bool Due(double seconds,double fps){
  while(submitted.TryPeek(out var first)&&seconds-first>=1)submitted.Dequeue();
  if(submitted.Count>=Math.Ceiling(Math.Clamp(fps,1,1000)))return false;
  submitted.Enqueue(seconds);return true;
 }
}
