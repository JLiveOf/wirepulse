using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
namespace WirePulse.WindowsSettings;

sealed partial class NativeEditorShell {
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetOwner(IntPtr hwnd,int index);

 [StructLayout(LayoutKind.Sequential)]struct ScreenPoint{public int X,Y;}
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(ScreenPoint point);
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 static IEnumerable<T> Controls<T>(DependencyObject parent)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T item)yield return item;foreach(var descendant in Controls<T>(child))yield return descendant;}}
 async Task SnapshotPanel(string path){
  var bitmap=new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();await bitmap.RenderAsync(panelRoot);var pixels=await bitmap.GetPixelsAsync();using var reader=DataReader.FromBuffer(pixels);var bytes=new byte[pixels.Length];reader.ReadBytes(bytes);
  string directory=System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;Directory.CreateDirectory(directory);var folder=await StorageFolder.GetFolderFromPathAsync(directory);var file=await folder.CreateFileAsync(System.IO.Path.GetFileName(path),CreationCollisionOption.ReplaceExisting);using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,bytes);await encoder.FlushAsync();
 }
 async Task Smoke(string output){
  poll.Stop();var checks=new List<string>();string? failure=null;
  Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output))!);void Trace(string value)=>File.AppendAllText(output+".trace.txt",value+"\n");void Pass(string value){checks.Add(value);Trace(value);}Trace("Smoke started");
  try{
   await Task.Delay(200);if(GetOwner(WindowNative.GetWindowHandle(panel),-8).ToInt64()!=state["owner"]!.GetValue<long>())throw new Exception("El panel WinUI no pertenece a la ventana de previsualización");
   ToggleProperties();await Task.Delay(80);if(IsWindowVisible(WindowNative.GetWindowHandle(panel)))throw new Exception("Properties does not hide the inspector");
   Apply(await Request(Command("editor-get")));if(IsWindowVisible(WindowNative.GetWindowHandle(panel)))throw new Exception("Polling reopens the hidden inspector");
   ToggleProperties();await Task.Delay(80);if(!IsWindowVisible(WindowNative.GetWindowHandle(panel)))throw new Exception("Propiedades no recupera el panel oculto");Pass("Properties toggles the WinUI inspector and polling preserves its hidden state");
   await Request(Command("editor-test-focus"));await Task.Delay(150);var position=panel.AppWindow.Position;if(GetAncestor(WindowFromPoint(new ScreenPoint{X=position.X+40,Y=position.Y+40}),2)!=WindowNative.GetWindowHandle(panel))throw new Exception("El lienzo cubre el panel de propiedades después de activarse");Pass("Inspector remains above the activated editor canvas");
   if(layers.Items.Count!=2)throw new Exception("Faltan capas nativas");Pass("Owned native WinUI inspector and toolbar");
   var sliders=Controls<Slider>(panelRoot).ToArray();var density=sliders.Single(s=>Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(s)==L10n.Text("Densidad de malla"));if(sliders.Length<6||Controls<NumberBox>(panelRoot).Any())throw new Exception("El inspector sigue usando campos numéricos en vez de deslizadores");
   int renderer=state["rendererIdentity"]!.GetValue<int>();density.Value=27;await Task.Delay(200);await Drain();Apply(await Request(Command("editor-get")));
   if(state["selected"]!["style"]!["density"]!.GetValue<double>()!=27||state["savedUnchanged"]!.GetValue<bool>()!=true||state["rendererIdentity"]!.GetValue<int>()!=renderer)throw new Exception("El deslizador no conserva el renderizador o modifica el diseño antes de Guardar");Pass("Native slider updates retained renderer without saving the draft");
   await Send(Command("editor-undo"));if(state["selected"]!["style"]!["density"]!.GetValue<double>()!=24)throw new Exception("Deshacer no restaura el valor anterior");
   await Send(Command("editor-redo"));if(state["selected"]!["style"]!["density"]!.GetValue<double>()!=27)throw new Exception("Rehacer no restaura el valor del deslizador");
   string gesture=Guid.NewGuid().ToString("N");foreach(double value in new[]{28d,29d}){var change=Command("editor-style","density",JsonValue.Create(value));change["gesture"]=gesture;await Send(change);}
   await Send(Command("editor-undo"));if(state["selected"]!["style"]!["density"]!.GetValue<double>()!=27)throw new Exception("Un gesto de deslizador crea varias acciones de deshacer");Pass("Undo/redo and one history entry per slider gesture");
   var order=state["items"]!.AsArray().Select(i=>i!["id"]!.GetValue<string>()).Reverse().ToArray();await Send(new(){["op"]="editor-order",["ids"]=new JsonArray(order.Select(id=>(JsonNode?)JsonValue.Create(id)).ToArray())});
   if(!state["items"]!.AsArray().Select(i=>i!["id"]!.GetValue<string>()).SequenceEqual(order))throw new Exception("El orden de capas no coincide con el orden visible");Pass("Arbitrary front-to-back layer order");
   var opacity=Controls<Slider>(panelRoot).Single(s=>Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(s)==L10n.Text("Opacidad"));opacity.Value=.64;await Task.Delay(160);await Drain();Apply(await Request(Command("editor-get")));if(Math.Abs(state["selected"]!["opacity"]!.GetValue<double>()-.64)>.001)throw new Exception("El deslizador de opacidad no se aplica");
   Trace("Selecting text");string orbId=selected,textId=state["items"]!.AsArray().Single(i=>i!["kind"]!.GetValue<string>()=="text")!["id"]!.GetValue<string>();await Send(Command("editor-select",id:textId));
   Trace("Text properties ready");var textSize=Controls<Slider>(panelRoot).Single(c=>Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(c)==L10n.Text("Tamaño de texto (0 = automático)"));textSize.Value=37;await Task.Delay(100);await Drain();
   var bold=Controls<ToggleSwitch>(panelRoot).Single(c=>c.Header?.ToString()==L10n.Text("Negrita"));bold.IsOn=true;
   var animation=Controls<ComboBox>(panelRoot).Single(c=>c.ItemsSource is string[] names&&names.Contains("Máquina de escribir"));animation.SelectedItem="Máquina de escribir";await Task.Delay(120);await Drain();Apply(await Request(Command("editor-get")));
   Trace("Expanding gradient");foreach(var expander in Controls<Expander>(panelRoot).Where(c=>c.Header?.ToString()==L10n.Text("Gradiente")))expander.IsExpanded=true;await Task.Delay(80);
   var gradient=Controls<ToggleSwitch>(panelRoot).Single(c=>c.Header?.ToString()==L10n.Text("Activar gradiente"));gradient.IsOn=true;var gradientBox=Controls<Expander>(panelRoot).Single(c=>c.Header?.ToString()==L10n.Text("Gradiente"));Controls<Slider>(gradientBox).Single().Value=90;await Task.Delay(120);await Drain();Apply(await Request(Command("editor-get")));
   if(state["selected"]!["textSize"]!.GetValue<double>()!=37||state["selected"]!["textBold"]!.GetValue<bool>()!=true||state["selected"]!["textAnimation"]!.GetValue<string>()!="typewriter"||state["selected"]!["gradients"]!["color"]!["enabled"]!.GetValue<bool>()!=true||state["selected"]!["gradients"]!["color"]!["angle"]!.GetValue<double>()!=90)throw new Exception("Native text formatting, animation or gradient controls did not reach the renderer");Pass("Native text formatting, animation and live gradient controls");Trace("Text snapshot begin");await SnapshotPanel(output+".text.png");Trace("Text snapshot end");await Send(Command("editor-select",id:orbId));
   await SnapshotPanel(output+".png");Pass("Native color, range and opacity controls render correctly");
  }catch(Exception ex){failure=ex.ToString();}
  Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output))!);await File.WriteAllTextAsync(output+".ui.json",JsonSerializer.Serialize(new{valid=failure==null,error=failure,checks},new JsonSerializerOptions{WriteIndented=true}));
  bool discard=args.Contains("--editor-discard-test")||failure!=null;await Send(Command(discard?"editor-discard":"editor-save"));
 }
}
