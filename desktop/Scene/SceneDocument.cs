using Avalonia;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;

public sealed class SceneItem {
 public string Id{get;set;}=Guid.NewGuid().ToString("N");public string Kind{get;set;}="orb";public string Name{get;set;}="Orbe";
 public double X{get;set;}=.35;public double Y{get;set;}=.3;public double Width{get;set;}=.3;public double Height{get;set;}=.4;
 public double Rotation{get;set;}
 public bool Visible{get;set;}=true;public bool Locked{get;set;}public bool KeepAspect{get;set;}=true;public double Opacity{get;set;}=1;
 public string Asset{get;set;}="";public string Text{get;set;}="Tu texto";public string Font{get;set;}="System";public string Color{get;set;}="#ffffff";
 public Dictionary<string,JsonNode?> Style{get;set;}=[];
 public Rect Bounds(Size viewport)=>new(X*viewport.Width,Y*viewport.Height,Width*viewport.Width,Height*viewport.Height);
 public void SetBounds(Rect rect,Size viewport){X=rect.X/viewport.Width;Y=rect.Y/viewport.Height;Width=rect.Width/viewport.Width;Height=rect.Height/viewport.Height;}
 public SceneItem Clone()=>JsonSerializer.Deserialize<SceneItem>(JsonSerializer.Serialize(this))!;
}
public sealed class SceneDocument {
 public int Version{get;set;}=1;public List<SceneItem> Items{get;set;}=[];
 public SceneDocument Clone()=>JsonSerializer.Deserialize<SceneDocument>(JsonSerializer.Serialize(this))!;
 public void Sanitize(){Items=Items.Where(x=>SceneKinds.Contains(x.Kind)).Take(256).ToList();var ids=new HashSet<string>();foreach(var x in Items){if(string.IsNullOrEmpty(x.Id)||!ids.Add(x.Id)){x.Id=Guid.NewGuid().ToString("N");ids.Add(x.Id);}x.X=Finite(x.X,-4,4,.35);x.Y=Finite(x.Y,-4,4,.3);x.Width=Finite(x.Width,.005,5,.3);x.Height=Finite(x.Height,.005,5,.3);x.Opacity=Finite(x.Opacity,0,1,1);x.Rotation=Finite(x.Rotation,-360000,360000,0)%360;if(!Avalonia.Media.Color.TryParse(x.Color,out _))x.Color="#ffffff";}}
 static double Finite(double n,double lo,double hi,double fallback)=>double.IsFinite(n)?Math.Clamp(n,lo,hi):fallback;
 public static readonly string[] SceneKinds=["orb","spectrum","card","clock","stars","mesh","image","text","widget"];
 public static readonly Dictionary<string,string> Names=new(){["orb"]="Orbe",["spectrum"]="Espectro",["card"]="Tarjeta musical",["clock"]="Reloj",["stars"]="Estrellas",["mesh"]="Malla animada",["image"]="Imagen / GIF",["text"]="Texto",["widget"]="Widget HTML"};
 public static SceneItem New(string kind,Size size,Settings settings){double height=kind switch{"orb"=>size.Height*.32,"spectrum"=>size.Height*.6,"card"=>size.Height*.18,"clock" or "text"=>size.Height*.08,_=>size.Height*.35};double width=kind switch{"orb"=>height,"spectrum"=>size.Width*.15,"card"=>size.Width*.4,"clock" or "text"=>size.Width*.3,_=>size.Width*.35};var item=new SceneItem{Kind=kind,Name=L10n.Text(Names[kind]),Font=settings.ClockFont,Color=settings.Text(kind=="clock"?"clockcolor":"wirecolor"),Style=settings.Values.ToDictionary(x=>x.Key,x=>x.Value?.DeepClone())};item.SetBounds(new Rect((size.Width-width)/2,(size.Height-height)/2,width,height),size);return item;}
 public static SceneDocument Migrate(Settings s,Size size){var scene=new SceneDocument();void Add(string kind,Rect rect,bool visible){var item=New(kind,size,s);item.SetBounds(rect,size);item.Visible=visible;scene.Items.Add(item);}
  double w=size.Width,h=size.Height,r=h*s.N("radius")/100,scale=s.N("cardscale"),cw=Math.Min(850,Math.Max(260,w*.44))*scale,ch=Math.Max(130,h*.21)*scale;
  Add(s.BackgroundStyle=="Malla"?"mesh":"stars",new Rect(0,0,w,h),s.On("bgshow"));
  Add("orb",new Rect(w*s.N("sphereX")/100-r,h*s.N("sphereY")/100-r,r*2,r*2),s.On("sphere"));
  Add("spectrum",new Rect(w*s.N("spectrumX")/100,h*s.N("spectrumY")/100,w*s.N("spectrumWidth")/100,h*s.N("spectrumHeight")/100),s.On("spectrum"));
  Add("card",new Rect(s.TopCentered?(w-cw)/2:w*s.N("cardX")/100,s.TopCentered?h*s.TopMargin/100:h-h*s.N("cardY")/100-ch,cw,ch),s.On("card"));
  double cs=s.N("clocksize");Add("clock",new Rect(Math.Clamp(w*s.N("clockX")/100-cs*1.8,0,Math.Max(0,w-cs*3.6)),Math.Clamp(h*s.N("clockY")/100-cs*.7,0,Math.Max(0,h-cs*1.4)),cs*3.6,cs*1.4),s.On("clockshow"));return scene;
 }
}
public readonly record struct SnapResult(Rect Rect,double? Vertical,double? Horizontal);
public static class SceneGeometry {
 public static SnapResult Move(Rect original,Vector delta,Size viewport,IEnumerable<Rect> others,bool snap){var rect=original.Translate(delta);double? vx=null,hy=null;
  if(snap){var xs=new List<double>{0,viewport.Width/2,viewport.Width};var ys=new List<double>{0,viewport.Height/2,viewport.Height};foreach(var box in others){xs.AddRange([box.Left,box.Center.X,box.Right]);ys.AddRange([box.Top,box.Center.Y,box.Bottom]);}var sx=Nearest([rect.Left,rect.Center.X,rect.Right],xs);var sy=Nearest([rect.Top,rect.Center.Y,rect.Bottom],ys);rect=rect.Translate(new Vector(sx.Offset,sy.Offset));vx=sx.Guide;hy=sy.Guide;}
  double x=Math.Clamp(rect.X,Math.Min(0,24-rect.Width),Math.Max(0,viewport.Width-24)),y=Math.Clamp(rect.Y,Math.Min(0,24-rect.Height),Math.Max(0,viewport.Height-24));return new(new Rect(x,y,rect.Width,rect.Height),vx,hy);
 }
 static (double Offset,double? Guide) Nearest(double[] edges,List<double> targets){double best=8,offset=0;double? guide=null;foreach(double target in targets)foreach(double edge in edges){double distance=Math.Abs(target-edge);if(distance<best){best=distance;offset=target-edge;guide=target;}}return(offset,guide);}
 public static Rect Resize(Rect original,Vector delta,int handle,bool aspect){double left=original.Left,top=original.Top,right=original.Right,bottom=original.Bottom;if(handle is 0 or 6 or 7)left+=delta.X;if(handle is 2 or 3 or 4)right+=delta.X;if(handle is 0 or 1 or 2)top+=delta.Y;if(handle is 4 or 5 or 6)bottom+=delta.Y;double w=Math.Max(16,right-left),h=Math.Max(16,bottom-top);if(aspect){double ratio=original.Width/original.Height;if(Math.Abs(delta.X/original.Width)>Math.Abs(delta.Y/original.Height))h=w/ratio;else w=h*ratio;if(handle is 0 or 6 or 7)left=original.Right-w;if(handle is 0 or 1 or 2)top=original.Bottom-h;}return new Rect(left,top,w,h);}
 public static Point Rotate(Point p,Point center,double degrees){double a=degrees*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),x=p.X-center.X,y=p.Y-center.Y;return new(center.X+x*c-y*s,center.Y+x*s+y*c);}
 public static Rect WorldBounds(Rect r,double angle){var points=Handles(r).Select(p=>Rotate(p,r.Center,angle)).ToArray();return new Rect(new Point(points.Min(p=>p.X),points.Min(p=>p.Y)),new Point(points.Max(p=>p.X),points.Max(p=>p.Y)));}
 public static bool Contains(Rect r,double angle,Point p)=>r.Contains(Rotate(p,r.Center,-angle));
 public static Rect ResizeRotated(Rect r,Vector delta,int handle,bool aspect,double angle){var origin=Rotate(new Point(delta.X,delta.Y),default,-angle);var resized=Resize(r,new Vector(origin.X,origin.Y),handle,aspect);var anchor=Rotate(Handles(r)[(handle+4)%8],r.Center,angle);var other=Rotate(Handles(resized)[(handle+4)%8],resized.Center,angle);return resized.Translate(anchor-other);}
 public static Point[] Handles(Rect r)=>[r.TopLeft,new(r.Center.X,r.Top),r.TopRight,new(r.Right,r.Center.Y),r.BottomRight,new(r.Center.X,r.Bottom),r.BottomLeft,new(r.Left,r.Center.Y)];
}
