using SkiaSharp;
namespace WirePulse;

public sealed record SpaceOptions(bool Visible,string Style,int Stars,double Spacing,double Speed,double Direction,double Boost,bool Reactive,double Opacity,double Line,SKColor Color){
 public static SpaceOptions Read(Settings s){var c=Avalonia.Media.Color.Parse(s.Text("bgcolor"));return new(s.On("bgshow"),s.BackgroundStyle,(int)s.N("bgdensity"),s.N("bgspacing"),s.N("bgspeed"),s.N("bgdirection"),s.N("bgboost"),s.On("bgreact"),s.N("bgopacity"),s.N("bgline"),new SKColor(c.R,c.G,c.B,c.A));}
}
public sealed class SpaceBackground:IDisposable {
 readonly BackgroundMotion motion=new();double pulse;readonly SKPaint paint=new(){IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeCap=SKStrokeCap.Round};readonly SKPath path=new();SKPoint[] points=[];SKPoint[] grid=[];
 public static double Wrap(double value,double extent)=>((value%extent)+extent)%extent;
 static double Seed(uint index,uint salt){uint x=index^salt;x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return(x&0xffffff)/16777216d;}
 void Capacity(int count){if(points.Length<count)points=new SKPoint[count];}
 public void Draw(SKCanvas c,double w,double h,double time,double dt,SpaceOptions o,double beat,double bpm,bool paused,bool gpu,bool light){if(!o.Visible)return;double target=o.Reactive?beat:0;pulse+=(target-pulse)*(1-Math.Exp(-Math.Clamp(dt,0,.1)*18));motion.Advance(dt,o.Speed,o.Direction,o.Reactive?o.Boost:0,pulse,bpm,paused);paint.Color=o.Color.WithAlpha((byte)(255*Math.Clamp(o.Opacity*(1+pulse*.8),0,1)));paint.StrokeWidth=(float)o.Line;
  if(o.Style=="Estrellas"){int count=Math.Clamp(o.Stars,10,400);if(light)count=Math.Min(count,80);Capacity(count*8);int starPoints=0;for(int i=0;i<count;i++){float x=(float)Wrap(Seed((uint)i,0x1234abcd)*w+motion.X,w),y=(float)Wrap(Seed((uint)i,0xbadf00d)*h+motion.Y,h),r=(float)((1.5+Seed((uint)i,0xabcdef01)*2)*(1+pulse*.5));points[starPoints++]=new(x-r,y);points[starPoints++]=new(x,y-r);points[starPoints++]=new(x,y-r);points[starPoints++]=new(x+r,y);points[starPoints++]=new(x+r,y);points[starPoints++]=new(x,y+r);points[starPoints++]=new(x,y+r);points[starPoints++]=new(x-r,y);}if(gpu)SkiaLines.Draw(c,points,starPoints,paint,path);else{path.Rewind();for(int i=0;i<starPoints;i+=2){path.MoveTo(points[i]);path.LineTo(points[i+1]);}c.DrawPath(path,paint);}return;}
  double spacing=Math.Clamp(o.Spacing,40,240);if(light)spacing=Math.Max(120,spacing);int cols=(int)Math.Ceiling(w/spacing)+3,rows=(int)Math.Ceiling(h/spacing)+3,countGrid=cols*rows;if(grid.Length<countGrid)grid=new SKPoint[countGrid];Capacity(countGrid*6);long startX=(long)Math.Floor(-motion.X/spacing)-1,startY=(long)Math.Floor(-motion.Y/spacing)-1;
  for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){double gx=startX+x,gy=startY+y,warp=spacing*.08*pulse;grid[y*cols+x]=new((float)(gx*spacing+motion.X+Math.Sin(gx*.71+gy*.93+time*.3)*warp),(float)(gy*spacing+motion.Y+Math.Cos(gx*.83-gy*.63+time*.3)*warp));}
  int n=0;for(int y=0;y<rows-1;y++)for(int x=0;x<cols-1;x++){var p=grid[y*cols+x];points[n++]=p;points[n++]=grid[y*cols+x+1];points[n++]=p;points[n++]=grid[(y+1)*cols+x];points[n++]=p;points[n++]=grid[(y+1)*cols+x+1];}
  if(gpu)SkiaLines.Draw(c,points,n,paint,path);else{path.Rewind();for(int i=0;i<n;i+=2){path.MoveTo(points[i]);path.LineTo(points[i+1]);}c.DrawPath(path,paint);}
 }
 public void Dispose(){paint.Dispose();path.Dispose();}
}
