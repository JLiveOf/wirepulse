namespace WirePulse;

/// Integrate velocity rather than multiplying absolute time: beats never teleport the grid.
public sealed class BackgroundMotion {
 public double X{get;private set;} public double Y{get;private set;}public double Speed{get;private set;}
 public void Advance(double dt,double baseSpeed,double direction,double boost,double beat,double bpm,bool paused){if(paused)return;dt=Math.Clamp(dt,0,.1);Speed=baseSpeed*(1+boost*Math.Clamp(beat,0,1)*(bpm>0?Math.Clamp(bpm/120,.5,2):1));double angle=direction*Math.PI/180;X+=Math.Cos(angle)*Speed*dt;Y+=Math.Sin(angle)*Speed*dt;}
}
