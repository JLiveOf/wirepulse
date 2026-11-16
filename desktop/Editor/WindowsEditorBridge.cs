#if WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;

public sealed partial class EditorWindow {
 bool useWindowsEditor,windowsCloseRequested;int windowsInspectorRevision;PixelRect windowsScreen;double windowsScaling;string? windowsGesture;string windowsOriginalSnapshot="";
 internal bool WindowsEditorEnabled=>useWindowsEditor;
 internal PixelRect WindowsScreen=>windowsScreen;
 internal Settings WindowsOriginal=>original;
 static readonly JsonSerializerOptions WindowsJson=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
 void WindowsCheckpoint(JsonObject request){string? gesture=request["gesture"]?.GetValue<string>();if(gesture==null||gesture!=windowsGesture)Checkpoint();windowsGesture=gesture;}
 JsonNode WindowsSnapshot()=>JsonSerializer.SerializeToNode(new{
  open=!finished&&IsVisible,savedUnchanged=JsonSerializer.Serialize(original.Scene)==windowsOriginalSnapshot,selectedId=canvas.SelectedId,selected=Selected,dirty=Snapshot()!=initial,revision=windowsInspectorRevision,
  items=Document.Items.AsEnumerable().Reverse(),defaults=draft.Values,kinds=SceneDocument.Names,snap=canvas.Snapping,canUndo=undo.Count>0,canRedo=redo.Count>0,
  closeRequested=windowsCloseRequested,owner=TryGetPlatformHandle()?.Handle.ToInt64()??0,hostProcess=Environment.ProcessId,
  screen=new{x=windowsScreen.X,y=windowsScreen.Y,width=windowsScreen.Width,height=windowsScreen.Height,scaling=windowsScaling},
  specs=draft.Specs.Where(s=>!LayoutKeys.Contains(s.Key)&&s.Kind is not("image" or "file")&&s.Key is not("sphere" or "spectrum" or "card" or "clockshow" or "bgshow")).Select(s=>new{key=s.Key,title=s.Title,kind=s.Kind,group=s.Group,min=s.Min,max=s.Max,step=s.Step,value=s.Value}),
  rendererIdentity=preview.GetVisualDescendants().OfType<SceneNode>().FirstOrDefault(n=>n.View?.Configuration.On("sphere")==true)?.View is{} view?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(view):0
 },WindowsJson)!;
 internal JsonNode WindowsRequest(JsonObject request){
  string op=request["op"]?.GetValue<string>()??"editor-get";
  string? id=request["id"]?.GetValue<string>();var item=id==null?Selected:Document.Items.FirstOrDefault(x=>x.Id==id);
  switch(op){
   case "editor-get":break;
   case "editor-select":canvas.SelectedId=id;RefreshList();Inspector();canvas.InvalidateVisual();windowsGesture=null;break;
   case "editor-style":{
    if(item==null)throw new InvalidOperationException("Selecciona un objeto");string key=request["key"]!.GetValue<string>();
    string group=item.Kind switch{"orb"=>"Orbe","spectrum"=>"Espectro","card"=>"Tarjeta","clock"=>"Reloj","stars" or "mesh"=>"Fondo animado",_=>""};
    var spec=draft.Specs.FirstOrDefault(s=>s.Key==key&&s.Group==group&&!LayoutKeys.Contains(key))??throw new ArgumentException("Propiedad no disponible");JsonNode? value;
    if(spec.Kind=="slider"){double number=request["value"]!.GetValue<double>();if(!double.IsFinite(number))throw new ArgumentException("Valor inválido");value=JsonValue.Create(Math.Clamp(number,spec.Min,spec.Max));}
    else if(spec.Kind is "bool" or "toggle")value=JsonValue.Create(request["value"]!.GetValue<bool>());
    else if(spec.Kind=="color"){string color=request["value"]!.GetValue<string>();if(!Avalonia.Media.Color.TryParse(color,out _))throw new ArgumentException("Color inválido");value=JsonValue.Create(color);}
    else throw new ArgumentException("Control no compatible");
    if(!JsonNode.DeepEquals(item.Style.GetValueOrDefault(key)??spec.Value,value)){WindowsCheckpoint(request);item.Style[key]=value;Changed();}break;
   }
   case "editor-property":{
    if(item==null)throw new InvalidOperationException("Selecciona un objeto");string key=request["key"]!.GetValue<string>();var value=request["value"];
    WindowsCheckpoint(request);
    switch(key){
     case "opacity":item.Opacity=Math.Clamp(value!.GetValue<double>(),0,1);break;
     case "rotation":{double angle=value!.GetValue<double>();if(!double.IsFinite(angle))throw new ArgumentException("Ángulo inválido");item.Rotation=angle%360;break;}
     case "visible":item.Visible=value!.GetValue<bool>();break;case "locked":item.Locked=value!.GetValue<bool>();break;case "keepAspect":item.KeepAspect=value!.GetValue<bool>();break;
     case "name":item.Name=value!.GetValue<string>();break;case "text":item.Text=value!.GetValue<string>();break;case "font":item.Font=value!.GetValue<string>();break;
     case "color":{string color=value!.GetValue<string>();if(!Avalonia.Media.Color.TryParse(color,out _))throw new ArgumentException("Color inválido");item.Color=color;break;}
     default:throw new ArgumentException("Propiedad desconocida");
    }Changed(true);break;
   }
   case "editor-order":{
    var ids=request["ids"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();var map=Document.Items.ToDictionary(x=>x.Id);
    if(ids.Length!=map.Count||ids.Distinct().Count()!=map.Count||ids.Any(x=>!map.ContainsKey(x)))throw new ArgumentException("Orden de capas inválido");
    if(!Document.Items.AsEnumerable().Reverse().Select(x=>x.Id).SequenceEqual(ids)){Checkpoint();Document.Items=ids.Reverse().Select(x=>map[x]).ToList();Changed(true);Inspector();}windowsGesture=null;break;
   }
   case "editor-undo":History(undo,redo);windowsGesture=null;break;case "editor-redo":History(redo,undo);windowsGesture=null;break;
   case "editor-duplicate":Duplicate();windowsGesture=null;break;case "editor-delete":Delete();windowsGesture=null;break;
   case "editor-center":if(item!=null){Checkpoint();item.X=(1-item.Width)/2;item.Y=(1-item.Height)/2;Changed();}break;
   case "editor-fit":if(item!=null){Checkpoint();var r=item.Bounds(canvas.Bounds.Size);double scale=Math.Min(canvas.Bounds.Width*.85/r.Width,canvas.Bounds.Height*.85/r.Height);item.SetBounds(new Rect((canvas.Bounds.Width-r.Width*scale)/2,(canvas.Bounds.Height-r.Height*scale)/2,r.Width*scale,r.Height*scale),canvas.Bounds.Size);Changed();}break;
   case "editor-front":case "editor-back":if(item!=null){Checkpoint();Document.Items.Remove(item);if(op=="editor-front")Document.Items.Add(item);else Document.Items.Insert(0,item);Changed(true);}break;
   case "editor-snap":canvas.Snapping=request["value"]!.GetValue<bool>();draft.EditorSnap=canvas.Snapping;canvas.InvalidateVisual();break;
   case "editor-add":{
    if(Document.Items.Count>=256)throw new InvalidOperationException("El diseño admite como máximo 256 objetos");string kind=request["kind"]!.GetValue<string>();if(!SceneDocument.SceneKinds.Contains(kind))throw new ArgumentException("Tipo desconocido");
    string? asset=request["asset"]?.GetValue<string>();if(kind is "image" or "widget"&&asset==null)throw new ArgumentException("Elige un archivo");
    var added=SceneDocument.New(kind,canvas.Bounds.Size,draft);if(asset!=null){added.Asset=asset;added.Name=Path.GetFileNameWithoutExtension(asset);if(kind=="image"){using var image=new ImageFrames(asset);added.Height=Math.Min(1,added.Width*canvas.Bounds.Width*image.Height/image.Width/canvas.Bounds.Height);added.Width=added.Height*canvas.Bounds.Height*image.Width/image.Height/canvas.Bounds.Width;}}
    Checkpoint();Document.Items.Add(added);canvas.SelectedId=added.Id;Changed(true);Inspector();break;
   }
   case "editor-save":finished=true;original.Scene=Document.Clone();original.EditorSnap=canvas.Snapping;original.Save();Close();break;
   case "editor-discard":finished=true;Close();break;
   case "editor-continue":windowsCloseRequested=false;break;
   default:throw new ArgumentException("Acción del editor desconocida");
  }
  return WindowsSnapshot();
 }
}

internal sealed partial class WindowsSettingsHost {
 Process? editorProcess;
 public bool ShowEditor(){
  string exe=Path.Combine(AppContext.BaseDirectory,"WindowsSettings","WirePulse.Settings.exe");if(!File.Exists(exe))return false;
  if(editorProcess is{HasExited:false}){editorProcess.Refresh();ShowWindow(editorProcess.MainWindowHandle,9);SetForegroundWindow(editorProcess.MainWindowHandle);return true;}
  serverTask??=Serve();var start=new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe)!};start.ArgumentList.Add("--pipe");start.ArgumentList.Add(pipe);start.ArgumentList.Add("--editor");
  if(Program.Option("--winui-editor-test-output") is string output){start.ArgumentList.Add("--editor-smoke-output");start.ArgumentList.Add(Path.GetFullPath(output));if(Program.Args.Contains("--editor-test-discard"))start.ArgumentList.Add("--editor-discard-test");}
  editorProcess=Process.Start(start);return editorProcess!=null;
 }
}
#endif
