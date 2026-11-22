using Avalonia;
using Avalonia.Media;
using SkiaSharp;
using System.Numerics;
namespace WirePulse;

public sealed record GradientSpec(bool Enabled=false,string End="#007aff",double Angle=0)
{
 public GradientSpec Clean()=>new(Enabled,Color.TryParse(End,out _)?End:"#007aff",double.IsFinite(Angle)?Angle%360:0);
 public (Point Start,Point End) Points(double w,double h){w=Math.Max(1,w);h=Math.Max(1,h);double a=Angle*Math.PI/180,x=Math.Cos(a),y=Math.Sin(a),span=Math.Abs(x)*w+Math.Abs(y)*h;return(new(w/2-x*span/2,h/2-y*span/2),new(w/2+x*span/2,h/2+y*span/2));}
 public IBrush Brush(string start,Size size=default){if(!Enabled)return new SolidColorBrush(Color.Parse(start));double w=Math.Max(1,size.Width),h=Math.Max(1,size.Height);var (a,b)=Points(w,h);return new LinearGradientBrush{StartPoint=new RelativePoint(a.X/w,a.Y/h,RelativeUnit.Relative),EndPoint=new RelativePoint(b.X/w,b.Y/h,RelativeUnit.Relative),GradientStops=new(){new(Color.Parse(start),0),new(Color.Parse(End),1)}};}
 public SKShader Shader(SKColor start,double w,double h){var(a,b)=Points(w,h);return SKShader.CreateLinearGradient(new((float)a.X,(float)a.Y),new((float)b.X,(float)b.Y),new[]{start,SKColor.Parse(End)},SKShaderTileMode.Clamp);}
 public Vector4 Sample(Vector4 start,double x,double y,double w,double h){double a=Angle*Math.PI/180,dx=Math.Cos(a),dy=Math.Sin(a);float t=(float)Math.Clamp(.5+((x-w/2)*dx+(y-h/2)*dy)/Math.Max(.0001,Math.Abs(dx)*w+Math.Abs(dy)*h),0,1);var end=Color.Parse(End);return Vector4.Lerp(start,new(end.R/255f,end.G/255f,end.B/255f,end.A/255f),t);}
}
public static class TextMotion
{
 public static readonly string[] Modes=["none","float","pulse","typewriter","scroll"];
 public static (double X,double Y,double Scale,double Opacity,int Characters) Frame(string mode,double time,double speed,double distance,int count){double t=Math.Max(0,time)*Math.Clamp(speed,.1,5),wave=Math.Sin(t*Math.PI*2/3);return mode switch{"float"=>(0,wave*distance,1,1,count),"pulse"=>(0,0,1+.05*wave,.75+.25*(wave+1)/2,count),"scroll"=>((1-(t/6%1)*2)*distance,0,1,1,count),"typewriter"=>(0,0,1,1,Math.Clamp((int)((t/5%1)*1.5*count),0,count)),_=>(0,0,1,1,count)};}
}
internal sealed class GradientShaderCache:IDisposable
{
 GradientSpec? spec;SKColor start;double width,height;SKShader? shader;
 public SKShader? Get(GradientSpec? next,SKColor color,double w,double h){if(next?.Enabled!=true){shader?.Dispose();shader=null;spec=null;return null;}if(spec!=next||start!=color||width!=w||height!=h){shader?.Dispose();spec=next;start=color;width=w;height=h;shader=next.Shader(color,w,h);}return shader;}
 public void Dispose()=>shader?.Dispose();
}
/// Cached palette and projection for the native line engines. Construct once per color group/frame.
internal readonly struct GradientSampler
{
 readonly Vector4 start,end;readonly float dx,dy,offset,inverse;
 public GradientSampler(GradientSpec spec,SKColor color,double w,double h){start=new(color.Red/255f,color.Green/255f,color.Blue/255f,color.Alpha/255f);var last=Color.Parse(spec.End);end=new(last.R/255f,last.G/255f,last.B/255f,last.A/255f);double a=spec.Angle*Math.PI/180;dx=(float)Math.Cos(a);dy=(float)Math.Sin(a);inverse=1/(float)Math.Max(.0001,Math.Abs(dx)*w+Math.Abs(dy)*h);offset=.5f-((float)w/2*dx+(float)h/2*dy)*inverse;}
 public Vector4 Sample(float x,float y)=>Vector4.Lerp(start,end,Math.Clamp((x*dx+y*dy)*inverse+offset,0,1));
}
