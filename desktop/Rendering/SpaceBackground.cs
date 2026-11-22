using SkiaSharp;
namespace WirePulse;

public sealed record SpaceOptions(bool Visible,string Style,int Stars,double Spacing,double Speed,double Direction,double Boost,bool Reactive,double Opacity,double Line,SKColor Color){
 public GradientSpec? Gradient{get;init;}
 public static SpaceOptions Read(Settings s){var c=Avalonia.Media.Color.Parse(s.Text("bgcolor"));return new(s.On("bgshow"),s.BackgroundStyle,(int)s.N("bgdensity"),s.N("bgspacing"),s.N("bgspeed"),s.N("bgdirection"),s.N("bgboost"),s.On("bgreact"),s.N("bgopacity"),s.N("bgline"),new SKColor(c.R,c.G,c.B,c.A)){Gradient=s.Gradient("bgcolor")};}
}
public sealed class SpaceBackground:IDisposable {
 readonly GradientShaderCache shader=new();readonly BackgroundMotion motion=new();readonly StarMotion stars=new();double pulse;readonly SKPaint paint=new(){IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeCap=SKStrokeCap.Round};readonly SKPath path=new();SKPoint[] points=[];SKPoint[] grid=[];
 public static double Wrap(double value,double extent)=>((value%extent)+extent)%extent;
 static double Seed(uint index,uint salt){uint x=index^salt;x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return(x&0xffffff)/16777216d;}
 void Capacity(int count){if(points.Length<count)points=new SKPoint[count];}
 public void Draw(SKCanvas c,double w,double h,double time,double dt,SpaceOptions o,double beat,double bpm,bool paused,bool gpu,bool light){if(!o.Visible)return;double target=o.Reactive?beat:0;pulse+=(target-pulse)*(1-Math.Exp(-Math.Clamp(dt,0,.1)*18));motion.Advance(dt,o.Speed,o.Direction,o.Reactive?o.Boost:0,pulse,bpm,paused);paint.Shader=shader.Get(o.Gradient,o.Color,w,h);paint.Color=(paint.Shader!=null?SKColors.White:o.Color).WithAlpha((byte)(255*Math.Clamp(o.Opacity*(1+pulse*.8),0,1)));paint.StrokeWidth=(float)o.Line;
  if(o.Style=="Estrellas"){int count=Math.Clamp(o.Stars,10,400);if(light)count=Math.Min(count,80);stars.Advance(count,dt,motion.Speed,o.Direction,paused);Capacity(count*16);int starPoints=0;for(int i=0;i<count;i++){var p=stars.Position(i,w,h);double r=(1.5+Seed((uint)i,0xabcdef01)*2)*(1+pulse*.5);for(int j=0;j<8;j++){var a=StarMotion.Ring[j];var b=StarMotion.Ring[j+1];points[starPoints++]=new((float)(p.X+a.X*r),(float)(p.Y+a.Y*r));points[starPoints++]=new((float)(p.X+b.X*r),(float)(p.Y+b.Y*r));}}if(gpu)SkiaLines.Draw(c,points,starPoints,paint,path);else{path.Rewind();for(int i=0;i<starPoints;i+=2){path.MoveTo(points[i]);path.LineTo(points[i+1]);}c.DrawPath(path,paint);}return;}
  double spacing=Math.Clamp(o.Spacing,40,240);if(light)spacing=Math.Max(120,spacing);int cols=(int)Math.Ceiling(w/spacing)+3,rows=(int)Math.Ceiling(h/spacing)+3,countGrid=cols*rows;if(grid.Length<countGrid)grid=new SKPoint[countGrid];Capacity(countGrid*6);long startX=(long)Math.Floor(-motion.X/spacing)-1,startY=(long)Math.Floor(-motion.Y/spacing)-1;
  for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){double gx=startX+x,gy=startY+y,warp=spacing*.08*pulse;grid[y*cols+x]=new((float)(gx*spacing+motion.X+Math.Sin(gx*.71+gy*.93+time*.3)*warp),(float)(gy*spacing+motion.Y+Math.Cos(gx*.83-gy*.63+time*.3)*warp));}
  int n=0;for(int y=0;y<rows-1;y++)for(int x=0;x<cols-1;x++){var p=grid[y*cols+x];points[n++]=p;points[n++]=grid[y*cols+x+1];points[n++]=p;points[n++]=grid[(y+1)*cols+x];points[n++]=p;points[n++]=grid[(y+1)*cols+x+1];}
  if(gpu)SkiaLines.Draw(c,points,n,paint,path);else{path.Rewind();for(int i=0;i<n;i+=2){path.MoveTo(points[i]);path.LineTo(points[i+1]);}c.DrawPath(path,paint);}
 }
 public void Dispose(){shader.Dispose();paint.Dispose();path.Dispose();}
}
