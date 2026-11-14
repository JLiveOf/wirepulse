namespace WirePulse;

/// Persistent particle offsets: each point has its own depth, heading and gentle inertial sway.
public sealed class StarMotion {
 public static readonly (double X,double Y)[] Ring=Enumerable.Range(0,9).Select(i=>(Math.Cos(i*Math.PI/4),Math.Sin(i*Math.PI/4))).ToArray();
 readonly double[] x=new double[400],y=new double[400];double time;
 public static double Seed(uint index,uint salt){uint v=index^salt;v^=v>>16;v*=0x7feb352d;v^=v>>15;v*=0x846ca68b;v^=v>>16;return(v&0xffffff)/16777216d;}
 public void Advance(int count,double dt,double speed,double direction,bool paused){if(paused)return;dt=Math.Clamp(dt,0,.1);time+=dt;for(int i=0;i<Math.Min(count,x.Length);i++){double depth=.55+Seed((uint)i,0xa12c)*1.1,phase=Seed((uint)i,0xb45d)*Math.PI*2,heading=direction*Math.PI/180+(Seed((uint)i,0xc78e)-.5)*.85+Math.Sin(time*(.22+depth*.2)+phase)*.22;x[i]+=Math.Cos(heading)*speed*depth*dt;y[i]+=Math.Sin(heading)*speed*depth*dt;}}
 public (double X,double Y) Position(int index,double width,double height)=>(SpaceBackground.Wrap(Seed((uint)index,0x1234abcd)*width+x[index],width),SpaceBackground.Wrap(Seed((uint)index,0xbadf00d)*height+y[index],height));
}
