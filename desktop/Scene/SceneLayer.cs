using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
namespace WirePulse;

public sealed class SceneLayer:Panel,IDisposable {
 readonly Settings settings;readonly Spectrum spectrum;readonly MediaService media;readonly Dictionary<string,SceneNode> nodes=[];readonly RenderStatistics stats=new();readonly Counter counter;double previous;
 public RenderStatistics Statistics=>nodes.Values.Where(n=>n.IsVisible&&n.IsAnimatedVisualizer&&n.View!=null).Select(n=>n.View!.Statistics).Where(s=>s.Frames>0).OrderByDescending(s=>s.Frames).FirstOrDefault()??stats;
 public string EngineStatus=>string.Join(" | ",nodes.Values.Where(n=>n.View!=null).Select(n=>n.View!.EngineStatus).Distinct());
 public SceneLayer(Settings settings,Spectrum spectrum,MediaService media){this.settings=settings;this.spectrum=spectrum;this.media=media;ClipToBounds=true;counter=new Counter(stats);Children.Add(counter);Refresh();}
 public void Refresh(){var items=settings.Scene?.Items??[];var ids=items.Select(x=>x.Id).ToHashSet();foreach(var id in nodes.Keys.Where(x=>!ids.Contains(x)).ToArray()){Children.Remove(nodes[id]);nodes[id].Dispose();nodes.Remove(id);}int index=0;foreach(var item in items){if(!nodes.TryGetValue(item.Id,out var node)){node=new SceneNode(item,settings,spectrum,media);nodes[item.Id]=node;}else node.Update(item);node.IsVisible=item.Visible;node.Opacity=item.Opacity;node.RenderTransformOrigin=RelativePoint.Center;if(node.RenderTransform is not RotateTransform rotation||rotation.Angle!=item.Rotation)node.RenderTransform=new RotateTransform(item.Rotation);if(!Children.Contains(node))Children.Insert(index,node);else if(Children.IndexOf(node)!=index)Children.Move(Children.IndexOf(node),index);index++;}InvalidateArrange();}
 protected override Size ArrangeOverride(Size finalSize){foreach(var item in settings.Scene?.Items??[]){if(nodes.TryGetValue(item.Id,out var node)){var r=item.Bounds(finalSize);node.Place(r,finalSize);node.Arrange(r);}}counter.Arrange(new Rect(finalSize));return finalSize;}
 public void Advance(double time){double dt=previous==0?1/60d:Math.Max(0,time-previous);previous=time;foreach(var item in settings.Scene?.Items??[]){if(nodes.TryGetValue(item.Id,out var node)&&item.Visible&&item.Opacity>0&&(item.Kind=="orb"||SceneGeometry.WorldBounds(item.Bounds(Bounds.Size),item.Rotation).Intersects(new Rect(Bounds.Size))))node.Advance(time,dt,settings.Paused);}stats.Record(0);counter.InvalidateVisual();}
 public void ResetClock(){previous=0;}
 public void SetFrameLimit(double fps){foreach(var node in nodes.Values)node.View?.SetFrameLimit(fps);}
 public void Adapt(double fps){foreach(var node in nodes.Values)node.View?.Adapt(fps);}
 public void Dispose(){foreach(var node in nodes.Values)node.Dispose();nodes.Clear();}
 sealed class Counter(RenderStatistics stats):Control{public override void Render(DrawingContext c){c.Custom(new CountOperation(new Rect(Bounds.Size),stats));}}
 sealed class CountOperation(Rect bounds,RenderStatistics stats):ICustomDrawOperation{public Rect Bounds=>bounds;public bool HitTest(Point p)=>false;public bool Equals(ICustomDrawOperation? o)=>false;public void Dispose(){}public void Render(ImmediateDrawingContext c){var feature=c.TryGetFeature<ISkiaSharpApiLeaseFeature>();if(feature==null)return;using var lease=feature.Lease();if(OperatingSystem.IsWindows()&&!GraphicsBackend.Observed){using var platform=lease.TryLeasePlatformGraphicsApi();GraphicsBackend.Observe(lease.GrContext,platform?.Context.GetType().FullName);}stats.Backend=GraphicsBackend.Description(lease.GrContext!=null);}}
}
public sealed class SceneNode:Decorator,IDisposable {
 SceneItem item;readonly Settings root;readonly Settings proxy;readonly Spectrum spectrum;readonly MediaService media;ImageFrames? image;AssetVisual? asset;TextBlock? text;Size previousSize;Rect renderFrame;string kind="",assetPath="";Dictionary<string,System.Text.Json.Nodes.JsonNode?>? applied;string profile="",font="";public bool IsAnimatedVisualizer=>kind is "orb" or "spectrum" or "mesh" or "stars";public WallpaperView? View{get;private set;}
 public SceneNode(SceneItem item,Settings root,Spectrum spectrum,MediaService media){this.item=item;this.root=root;this.spectrum=spectrum;this.media=media;proxy=root.Preview();proxy.Scene=null;ClipToBounds=true;Update(item);}
 public void Update(SceneItem next){
  item=next;ClipToBounds=next.Kind!="orb";
  bool rebuild=kind!=next.Kind||((next.Kind is "image" or "widget")&&assetPath!=next.Asset);
  if(rebuild){kind=next.Kind;assetPath=next.Asset;View?.Dispose();image?.Dispose();asset?.Dispose();View=null;image=null;asset=null;text=null;previousSize=default;applied=null;
   if(kind is "image" or "widget"){try{if(kind=="image"){image=new ImageFrames(next.Asset,asynchronous:true);asset=new AssetVisual(image);}else asset=new AssetVisual(next.Asset,spectrum);Child=asset;}catch(Exception e){Child=new TextBlock{Text="No se puede mostrar "+next.Name+"\n"+e.Message,Foreground=Brushes.Orange};}return;}
   if(kind=="text"){text=new TextBlock{TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};Child=text;}
  }
  if(kind is "image" or "widget")return;
  if(text!=null){if(text.Text!=next.Text){text.Text=next.Text;previousSize=default;}var color=Color.Parse(next.Color);if(text.Foreground is not SolidColorBrush ink||ink.Color!=color)text.Foreground=new SolidColorBrush(color);if(font!=next.Font){font=next.Font;text.FontFamily=font=="System"?FontFamily.Default:new FontFamily(font);}Resize(Bounds.Size);return;}
  bool changed=applied==null||profile!=root.RenderProfile||font!=next.Font;
  // Compare values without serializing the scene or replacing retained renderers.
  foreach(var pair in root.Values){var value=next.Style.GetValueOrDefault(pair.Key)??pair.Value;if(pair.Key is "sensitivity" or "bass" or "gate" or "smoothing" or "fps" or "unlimitedfps" or "quality")value=pair.Value;if(applied==null||!applied.TryGetValue(pair.Key,out var old)||!System.Text.Json.Nodes.JsonNode.DeepEquals(old,value))changed=true;}
  if(!changed)return;
  applied=root.Values.ToDictionary(p=>p.Key,p=>(next.Style.GetValueOrDefault(p.Key)??p.Value)?.DeepClone());foreach(string key in new[]{"sensitivity","bass","gate","smoothing","fps","unlimitedfps","quality"})applied[key]=root.Values[key]?.DeepClone();
  foreach(var pair in applied)if(!EditorWindow.LayoutKeys.Contains(pair.Key))proxy.Values[pair.Key]=pair.Value?.DeepClone();
  font=next.Font;profile=root.RenderProfile;proxy.ClockFont=font;proxy.RenderProfile=profile;proxy.Scene=null;
  foreach(string key in new[]{"sphere","spectrum","card","clockshow","bgshow"})proxy.Values[key]=System.Text.Json.Nodes.JsonValue.Create(false);
  proxy.Values[kind switch{"orb"=>"sphere","spectrum"=>"spectrum","card"=>"card","clock"=>"clockshow",_=>"bgshow"}]=System.Text.Json.Nodes.JsonValue.Create(true);proxy.BackgroundStyle=kind=="mesh"?"Malla":"Estrellas";
  if(View==null){View=new WallpaperView(proxy,spectrum,media,transparent:true,cardFillsBounds:kind=="card");Child=View;Resize(Bounds.Size);}else proxy.Save();
 }
 public void Place(Rect nominal,Size viewport){if(item.Kind=="orb"&&View!=null){double radius=Math.Min(nominal.Width,nominal.Height)/2,reach=radius*(1+proxy.N("pulse"))*(1+.38*proxy.N("deformation"))+proxy.N("glow")+4;var clip=new Rect(viewport);if(item.Rotation!=0){var corners=new[]{clip.TopLeft,clip.TopRight,clip.BottomLeft,clip.BottomRight}.Select(p=>SceneGeometry.Rotate(p,nominal.Center,-item.Rotation)).ToArray();clip=new Rect(new Point(corners.Min(p=>p.X),corners.Min(p=>p.Y)),new Point(corners.Max(p=>p.X),corners.Max(p=>p.Y)));}var expanded=new Rect(nominal.Center.X-reach,nominal.Center.Y-reach,reach*2,reach*2).Intersect(clip);if(expanded.Width<1||expanded.Height<1)expanded=new Rect(nominal.X,nominal.Y,1,1);var local=expanded.Translate(new Vector(-nominal.X,-nominal.Y));if(renderFrame!=local||previousSize!=nominal.Size){renderFrame=local;previousSize=nominal.Size;proxy.Values["sphereX"]=System.Text.Json.Nodes.JsonValue.Create((nominal.Width/2-local.X)/local.Width*100);proxy.Values["sphereY"]=System.Text.Json.Nodes.JsonValue.Create((nominal.Height/2-local.Y)/local.Height*100);proxy.Values["radius"]=System.Text.Json.Nodes.JsonValue.Create(radius/local.Height*100);proxy.Save();InvalidateArrange();}}else{renderFrame=new Rect(nominal.Size);Resize(nominal.Size);}}
 protected override Size ArrangeOverride(Size size){if(item.Kind=="orb"&&View!=null){View.Arrange(renderFrame);return size;}return base.ArrangeOverride(size);}
 public void Resize(Size size){if(size.Width<=0||size.Height<=0||size==previousSize)return;previousSize=size;if(text!=null)text.FontSize=Math.Clamp(size.Height*.65/Math.Max(1,item.Text.Count(c=>c=='\n')+1),8,512);if(View==null)return;proxy.Values["sphereX"]=System.Text.Json.Nodes.JsonValue.Create(50d);proxy.Values["sphereY"]=System.Text.Json.Nodes.JsonValue.Create(50d);proxy.Values["radius"]=System.Text.Json.Nodes.JsonValue.Create(Math.Min(size.Width,size.Height)/size.Height*50);foreach(string key in new[]{"spectrumX","spectrumY"})proxy.Values[key]=System.Text.Json.Nodes.JsonValue.Create(0d);foreach(string key in new[]{"spectrumWidth","spectrumHeight"})proxy.Values[key]=System.Text.Json.Nodes.JsonValue.Create(100d);proxy.Values["clockX"]=System.Text.Json.Nodes.JsonValue.Create(50d);proxy.Values["clockY"]=System.Text.Json.Nodes.JsonValue.Create(50d);proxy.Values["clocksize"]=System.Text.Json.Nodes.JsonValue.Create(size.Height*.72);proxy.Save();}
 public void Advance(double time,double dt,bool paused){proxy.Paused=paused;View?.Advance(time);asset?.Advance(dt,paused);}
 public void Dispose(){View?.Dispose();image?.Dispose();asset?.Dispose();}
}
public sealed class AssetVisual:Control,IDisposable {
 readonly ImageFrames? image;readonly HtmlWidget? widget;
 public AssetVisual(ImageFrames image){this.image=image;image.FrameReady+=FrameReady;}
 void FrameReady()=>Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
 public AssetVisual(string path,Spectrum spectrum){widget=new HtmlWidget(path,spectrum);widget.Changed+=()=>Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);}
 public void Advance(double dt,bool paused){image?.Update(dt,paused);widget?.Resize(Bounds.Size);widget?.Tick(paused);}
 public override void Render(DrawingContext c){if(image!=null)c.Custom(new ImageOperation(image,new Rect(Bounds.Size)));else if(widget?.Image is{} frame)c.DrawImage(frame,new Rect(frame.Size),new Rect(Bounds.Size));else{c.DrawRectangle(new SolidColorBrush(Color.Parse("#88262b35")),null,new Rect(Bounds.Size));var label=new FormattedText(widget?.Status??"Cargando…",System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,Typeface.Default,14,Brushes.White);c.DrawText(label,new Point(8,8));}}
 public void Dispose(){if(image!=null)image.FrameReady-=FrameReady;widget?.Dispose();}
 sealed class ImageOperation(ImageFrames image,Rect bounds):ICustomDrawOperation {public Rect Bounds=>bounds;public bool HitTest(Point p)=>false;public bool Equals(ICustomDrawOperation? o)=>false;public void Dispose(){}public void Render(ImmediateDrawingContext c){var feature=c.TryGetFeature<ISkiaSharpApiLeaseFeature>();if(feature==null)return;using var lease=feature.Lease();image.Draw(lease.SkCanvas,new(0,0,(float)bounds.Width,(float)bounds.Height));}}
}
