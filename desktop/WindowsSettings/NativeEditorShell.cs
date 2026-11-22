using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;
using IOPath=System.IO.Path;
namespace WirePulse.WindowsSettings;

// WinUI owns the visible editor controls; the rendering process owns the draft and history.
sealed partial class NativeEditorShell {
 readonly Window panel=new(),toolbar=new();readonly Grid panelRoot=new();readonly StackPanel properties=new(){Spacing=14};
 readonly ListView layers=new(){SelectionMode=ListViewSelectionMode.Single,CanReorderItems=true,CanDragItems=true,AllowDrop=true,MaxHeight=242};
 readonly InfoBar error=new(){IsOpen=false,IsClosable=true};readonly CommandBar commands=new(){IsDynamicOverflowEnabled=true,DefaultLabelPosition=CommandBarDefaultLabelPosition.Right};
 readonly DispatcherTimer poll=new(){Interval=TimeSpan.FromMilliseconds(150)},flush=new(){Interval=TimeSpan.FromMilliseconds(60)};
 readonly SemaphoreSlim transport=new(1);readonly Dictionary<string,JsonObject> pending=[];readonly string pipe;readonly string[] args;
 JsonObject state=new();Task writes=Task.CompletedTask;bool updating,closing,busy,confirming;string selected="",layerKey="";int revision=-1,hostProcess;AppBarButton? undoButton,redoButton;AppBarToggleButton? snapButton;
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]static extern IntPtr SetOwner(IntPtr hwnd,int index,IntPtr value);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")]static extern bool ReleaseCapture();
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
 public NativeEditorShell(string[] args){
  this.args=args;int index=Array.IndexOf(args,"--pipe");pipe=index>=0&&index+1<args.Length?args[index+1]:throw new ArgumentException("Falta la conexión con WirePulse");
  panel.Title=L10n.Text("WirePulse · Objetos");toolbar.Title=L10n.Text("WirePulse · Herramientas del editor");
  foreach(var window in new[]{panel,toolbar}){window.AppWindow.IsShownInSwitchers=false;if(window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter){presenter.IsResizable=false;presenter.IsMaximizable=false;presenter.IsMinimizable=false;presenter.IsAlwaysOnTop=true;presenter.SetBorderAndTitleBar(false,false);}window.AppWindow.Closing+=async(_,e)=>{if(closing)return;e.Cancel=true;await Cancel();};}
  panelRoot.Background=Brush("#242424");panelRoot.Padding=new Thickness(16);panelRoot.RowSpacing=12;panelRoot.RequestedTheme=ElementTheme.Dark;
  panelRoot.RowDefinitions.Add(new(){Height=GridLength.Auto});panelRoot.RowDefinitions.Add(new(){Height=GridLength.Auto});panelRoot.RowDefinitions.Add(new(){Height=GridLength.Auto});panelRoot.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  var header=new StackPanel{Spacing=6};var title=new TextBlock{Text=L10n.Text("Objetos"),FontSize=22,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold};header.Children.Add(title);header.Children.Add(Label("Arrastra para ordenar · arriba queda delante",true));
  title.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(title).Properties.IsLeftButtonPressed){ReleaseCapture();SendMessage(WindowNative.GetWindowHandle(panel),0xA1,new IntPtr(2),IntPtr.Zero);}};
  panelRoot.Children.Add(header);Grid.SetRow(error,1);panelRoot.Children.Add(error);Grid.SetRow(layers,2);panelRoot.Children.Add(layers);
  var scroll=new ScrollViewer{Content=properties,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,3);panelRoot.Children.Add(scroll);panel.Content=panelRoot;
  toolbar.Content=new Border{Background=Brush("#242424"),CornerRadius=new CornerRadius(12),Padding=new Thickness(6),Child=commands,RequestedTheme=ElementTheme.Dark};
  layers.SelectionChanged+=async(_,_)=>{if(!updating&&layers.SelectedItem is ListViewItem row&&row.Tag is string id)await Send(Command("editor-select",id:id));};
  layers.DragItemsCompleted+=async(_,_)=>{if(updating)return;await Send(new(){["op"]="editor-order",["ids"]=new JsonArray(layers.Items.Cast<ListViewItem>().Select(x=>(JsonNode?)JsonValue.Create((string)x.Tag)).ToArray())});};
  poll.Tick+=async(_,_)=>{if(closing||busy)return;busy=true;try{Apply(await Request(Command("editor-get")));}catch(Exception ex){if(!HostAlive())CloseAll();else Error(ex.Message);}finally{busy=false;}};
  flush.Tick+=(_,_)=>{if(pending.Count>0&&writes.IsCompleted)writes=Flush();};
  panelRoot.KeyDown+=Keys;commands.KeyDown+=Keys;
 }
 public async void Start(){try{
  state=await Request(Command("editor-get"));if(state["open"]?.GetValue<bool>()!=true){CloseAll();return;}hostProcess=state["hostProcess"]!.GetValue<int>();
  var screen=state["screen"]!;double scaling=screen["scaling"]!.GetValue<double>();int x=screen["x"]!.GetValue<int>(),y=screen["y"]!.GetValue<int>(),w=screen["width"]!.GetValue<int>(),h=screen["height"]!.GetValue<int>();int margin=(int)Math.Round(16*scaling),width=Math.Min(w-margin*2,(int)Math.Round(340*scaling)),height=Math.Max(240,h-(int)Math.Round(120*scaling));
  panel.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x+w-width-margin,y+margin,width,height));int barWidth=Math.Min(w-margin*2,(int)Math.Round(1080*scaling)),barHeight=(int)Math.Round(76*scaling);toolbar.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x+(w-barWidth)/2,y+h-barHeight-margin,barWidth,barHeight));
  var owner=new IntPtr(state["owner"]!.GetValue<long>());foreach(var window in new[]{panel,toolbar})SetOwner(WindowNative.GetWindowHandle(window),-8,owner);
  BuildToolbar();Apply(state);ShowProperties();toolbar.AppWindow.Show();toolbar.Activate();poll.Start();flush.Start();
  int test=Array.IndexOf(args,"--editor-smoke-output");if(test>=0&&test+1<args.Length)await Smoke(args[test+1]);
 }catch(Exception ex){File.WriteAllText(IOPath.Combine(IOPath.GetTempPath(),"WirePulse-editor-error.txt"),ex.ToString());Error(ex.Message);ShowProperties();}}
 bool HostAlive(){try{return hostProcess==0||!Process.GetProcessById(hostProcess).HasExited;}catch{return false;}}
 static SolidColorBrush Brush(string color)=>new(ParseColor(color));
 static Color ParseColor(string color){
  if(!color.StartsWith('#')){if(typeof(Microsoft.UI.Colors).GetProperty(color)?.GetValue(null) is Color named)return named;return Microsoft.UI.Colors.White;}
  string hex=color[1..];if(hex.Length==3)hex=string.Concat(hex.Select(c=>new string(c,2)));if(hex.Length==6)hex="FF"+hex;
  if(hex.Length!=8)throw new ArgumentException("Color inválido");return Color.FromArgb(Convert.ToByte(hex[..2],16),Convert.ToByte(hex.Substring(2,2),16),Convert.ToByte(hex.Substring(4,2),16),Convert.ToByte(hex.Substring(6,2),16));
 }
 static TextBlock Label(string text,bool secondary=false)=>new(){Text=L10n.Text(text),FontSize=secondary?12:14,Foreground=Brush(secondary?"#AAAAAA":"#F2F2F2"),TextWrapping=TextWrapping.Wrap};
 static string Format(double value,double step)=>value.ToString(step<.01?"0.000":step<1?"0.00":"0",CultureInfo.CurrentCulture);
 JsonObject Command(string op,string? key=null,JsonNode? value=null,string? id=null)=>new(){["op"]=op,["key"]=key,["value"]=value,["id"]=id??(selected.Length>0?selected:null)};
 async Task<JsonObject> Request(JsonObject request){await transport.WaitAsync();try{
  using var timeout=new CancellationTokenSource(request["op"]?.GetValue<string>()=="editor-add"?TimeSpan.FromMinutes(2):TimeSpan.FromSeconds(8));
  await using var client=new NamedPipeClientStream(".",pipe,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await client.ConnectAsync(timeout.Token);
  using var writer=new StreamWriter(client,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};using var reader=new StreamReader(client,Encoding.UTF8,leaveOpen:true);await writer.WriteLineAsync(request.ToJsonString());
  var result=JsonNode.Parse(await reader.ReadLineAsync(timeout.Token)??"{}")!.AsObject();if(result["error"] is{} e)throw new InvalidOperationException(e.GetValue<string>());return result;
 }finally{transport.Release();}}
 async Task Send(JsonObject request){try{Apply(await Request(request));}catch(Exception ex){Error(ex.Message);}}
 void Error(string text){error.Title=L10n.Text("No se pudo aplicar el cambio");error.Message=L10n.Text(text);error.Severity=InfoBarSeverity.Error;error.IsOpen=true;}
 async Task Flush(){var batch=pending.Values.ToArray();pending.Clear();foreach(var request in batch)await Send(request);}
 async Task Drain(){await writes;await Flush();}
 void Queue(string op,string key,JsonNode value,string gesture){var request=Command(op,key,value);request["gesture"]=gesture;pending[selected+"/"+key]=request;}
 void Apply(JsonObject snapshot){
  state=snapshot;if(snapshot["open"]?.GetValue<bool>()!=true){CloseAll();return;}
  updating=true;
  try{
   var items=snapshot["items"]!.AsArray();string key=string.Join('|',items.Select(n=>$"{n!["id"]}:{n["name"]}:{n["locked"]}:{n["visible"]}"));string next=snapshot["selectedId"]?.GetValue<string>()??"";
   if(key!=layerKey){layerKey=key;layers.Items.Clear();foreach(var item in items.OfType<JsonObject>()){
    string id=item["id"]!.GetValue<string>();bool locked=item["locked"]!.GetValue<bool>();var row=new Grid{ColumnSpacing=8,Padding=new Thickness(2,4,2,4)};row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
    row.Children.Add(new FontIcon{Glyph="\uE700",FontSize=16,VerticalAlignment=VerticalAlignment.Center});var name=new TextBlock{Text=(item["visible"]!.GetValue<bool>()?"● ":"◌ ")+item["name"]!.GetValue<string>()};name.TextTrimming=TextTrimming.CharacterEllipsis;name.TextWrapping=TextWrapping.NoWrap;name.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(name,1);row.Children.Add(name);
    var padlock=new Button{Content=new FontIcon{Glyph=locked?"\uE72E":"\uE785",FontSize=14},Padding=new Thickness(8)};AutomationProperties.SetName(padlock,(locked?"Desbloquear ":"Bloquear ")+item["name"]!.GetValue<string>());padlock.Click+=async(_,e)=>{await Send(Command("editor-select",id:id));await Send(Command("editor-property","locked",JsonValue.Create(!locked),id));};Grid.SetColumn(padlock,2);row.Children.Add(padlock);layers.Items.Add(new ListViewItem{Content=row,Tag=id,HorizontalContentAlignment=HorizontalAlignment.Stretch});
   }}
   layers.SelectedItem=layers.Items.Cast<ListViewItem>().FirstOrDefault(x=>(string)x.Tag==next);
   int nextRevision=snapshot["revision"]!.GetValue<int>();if(next!=selected||nextRevision!=revision){selected=next;revision=nextRevision;BuildProperties();}
   if(undoButton!=null)undoButton.IsEnabled=snapshot["canUndo"]!.GetValue<bool>();if(redoButton!=null)redoButton.IsEnabled=snapshot["canRedo"]!.GetValue<bool>();if(snapButton!=null)snapButton.IsChecked=snapshot["snap"]!.GetValue<bool>();
  }finally{updating=false;}
  if(snapshot["closeRequested"]?.GetValue<bool>()==true&&!confirming)_=Cancel();
 }
 AppBarButton Action(string label,string glyph,Func<Task> action){var button=new AppBarButton{Label=L10n.Text(label),Icon=new FontIcon{Glyph=glyph},IsCompact=false};button.Click+=async(_,_)=>{await Drain();await action();};commands.PrimaryCommands.Add(button);return button;}
 void BuildToolbar(){
  commands.PrimaryCommands.Clear();var add=new AppBarButton{Label=L10n.Text("Añadir"),Icon=new SymbolIcon(Symbol.Add)};var menu=new MenuFlyout();foreach(var pair in state["kinds"]!.AsObject()){string kind=pair.Key;var item=new MenuFlyoutItem{Text=L10n.Text(pair.Value!.GetValue<string>())};item.Click+=async(_,_)=>await Add(kind);menu.Items.Add(item);}var folder=new MenuFlyoutItem{Text=L10n.Text("Widget desde carpeta…")};folder.Click+=async(_,_)=>await Add("widget",true);menu.Items.Add(folder);add.Flyout=menu;commands.PrimaryCommands.Add(add);
  undoButton=Action("Deshacer","\uE7A7",()=>Send(Command("editor-undo")));redoButton=Action("Rehacer","\uE7A6",()=>Send(Command("editor-redo")));
  snapButton=new AppBarToggleButton{Label=L10n.Text("Ajustar a guías"),Icon=new FontIcon{Glyph="\uE8A9"},IsChecked=state["snap"]!.GetValue<bool>()};snapButton.Click+=async(_,_)=>{if(!updating)await Send(Command("editor-snap",value:JsonValue.Create(snapButton.IsChecked==true)));};commands.PrimaryCommands.Add(snapButton);
  Action("Eliminar objeto","\uE74D",()=>Send(Command("editor-delete")));Action("Propiedades","\uE713",()=>{ToggleProperties();return Task.CompletedTask;});Action("Cancelar","\uE711",Cancel);
  var save=Action("Guardar diseño","\uE74E",async()=>{await Send(Command("editor-save"));});save.Background=Brush("#007AFF");save.Foreground=Brush("#FFFFFF");
 }
 async Task Add(string kind,bool folder=false){try{
  var request=Command("editor-add");request["kind"]=kind;if(kind is "image" or "widget"){
   string? path;if(folder){var picker=new FolderPicker();InitializeWithWindow.Initialize(picker,WindowNative.GetWindowHandle(panel));picker.FileTypeFilter.Add("*");path=(await picker.PickSingleFolderAsync())?.Path;}
   else{var picker=new FileOpenPicker();InitializeWithWindow.Initialize(picker,WindowNative.GetWindowHandle(panel));foreach(var extension in kind=="image"?new[]{".png",".jpg",".jpeg",".webp",".bmp",".gif"}:new[]{".html",".htm",".zip"})picker.FileTypeFilter.Add(extension);path=(await picker.PickSingleFileAsync())?.Path;}
   if(path==null)return;request["path"]=path;
  }await Drain();await Send(request);
 }catch(Exception ex){Error(ex.Message);}}
 void Toggle(string label,bool value,string key,bool style=false){var control=new ToggleSwitch{Header=L10n.Text(label),IsOn=value,OnContent=L10n.Text("Activado"),OffContent=L10n.Text("Desactivado")};control.Toggled+=async(_,_)=>{if(!updating)await Send(Command(style?"editor-style":"editor-property",key,JsonValue.Create(control.IsOn)));};properties.Children.Add(control);}
 void Range(string title,string key,double value,double min,double max,double step,bool style){
  var stack=new StackPanel{Spacing=4};var header=new Grid();header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.Children.Add(Label(title));var number=Label(Format(value,step),true);Grid.SetColumn(number,1);header.Children.Add(number);stack.Children.Add(header);
  var slider=new Slider{Minimum=min,Maximum=max,StepFrequency=step,Value=value};AutomationProperties.SetName(slider,L10n.Text(title));slider.Resources["SliderTrackValueFill"]=Brush("#007AFF");slider.Resources["SliderThumbBackground"]=Brush("#007AFF");string? gesture=null;
  slider.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,_)=>gesture=Guid.NewGuid().ToString("N")),true);slider.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler((_,_)=>gesture=null),true);slider.PointerCaptureLost+=(_,_)=>gesture=null;
  slider.ValueChanged+=(_,_)=>{number.Text=Format(slider.Value,step);if(!updating)Queue(style?"editor-style":"editor-property",key,JsonValue.Create(slider.Value)!,gesture??Guid.NewGuid().ToString("N"));};stack.Children.Add(slider);properties.Children.Add(stack);
 }
 void TextInput(string label,string key,string value,bool multiline=false){var stack=new StackPanel{Spacing=6};stack.Children.Add(Label(label));var box=new TextBox{Text=value,AcceptsReturn=multiline,TextWrapping=multiline?TextWrapping.Wrap:TextWrapping.NoWrap};box.LostFocus+=async(_,_)=>{if(box.Text!=value){value=box.Text;await Send(Command("editor-property",key,JsonValue.Create(box.Text)));}};stack.Children.Add(box);properties.Children.Add(stack);}
 void ColorInput(string title,string key,string color,bool style){
  var grid=new Grid{ColumnSpacing=10};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var label=Label(title);label.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(label);
  var swatch=new Border{Width=38,Height=20,CornerRadius=new CornerRadius(6),Background=Brush(color)};var button=new Button{Content=swatch};AutomationProperties.SetName(button,L10n.Text(title));button.Click+=async(_,_)=>{try{var picker=new ColorPicker{Color=ParseColor(color),IsAlphaEnabled=true,IsMoreButtonVisible=false};var dialog=new ContentDialog{Title=L10n.Text(title),Content=picker,PrimaryButtonText=L10n.Text("Aplicar"),CloseButtonText=L10n.Text("Cancelar"),DefaultButton=ContentDialogButton.Primary,XamlRoot=panelRoot.XamlRoot};string id=selected;if(await dialog.ShowAsync()==ContentDialogResult.Primary){var c=picker.Color;color=$"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";swatch.Background=Brush(color);await Send(Command(style?"editor-style":"editor-property",key,JsonValue.Create(color),id));}}catch(Exception ex){Error(ex.Message);}};Grid.SetColumn(button,1);grid.Children.Add(button);properties.Children.Add(grid);
 }
 void BuildProperties(){
  properties.Children.Clear();if(state["selected"] is not JsonObject item){properties.Children.Add(Label("Selecciona un objeto para editarlo. Arrastra sus bordes para cambiar el tamaño.",true));return;}
  TextInput("Nombre","name",item["name"]!.GetValue<string>());Toggle("Visible",item["visible"]!.GetValue<bool>(),"visible");Toggle("Bloquear posición",item["locked"]!.GetValue<bool>(),"locked");Toggle("Mantener proporciones",item["keepAspect"]!.GetValue<bool>(),"keepAspect");
  Range("Ángulo (grados)","rotation",item["rotation"]!.GetValue<double>(),-360,360,1,false);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};foreach(var action in new[]{("Duplicar","editor-duplicate"),("Centrar","editor-center"),("Encajar","editor-fit")}){var button=new Button{Content=action.Item1};button.Click+=async(_,_)=>{await Drain();await Send(Command(action.Item2));};actions.Children.Add(button);}properties.Children.Add(actions);
  int generalCount=properties.Children.Count;
  string kind=item["kind"]!.GetValue<string>();if(kind=="text"){TextInput("Texto","text",item["text"]!.GetValue<string>(),true);ColorInput("Color del texto","color",item["color"]!.GetValue<string>(),false);}
  if(kind is "text" or "clock"){properties.Children.Add(Label("Fuente"));using var fonts=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");var names=new[]{"System","Segoe UI","Arial","Consolas"}.Concat(fonts?.GetValueNames().Select(x=>x.Replace(" (TrueType)","").Replace(" (OpenType)",""))??[]).Distinct().Order().ToArray();var picker=new ComboBox{ItemTemplate=LocalizedChoices.Template,ItemsSource=names,SelectedItem=item["font"]!.GetValue<string>(),HorizontalAlignment=HorizontalAlignment.Stretch};picker.SelectionChanged+=async(_,_)=>{if(!updating&&picker.SelectedItem is string font)await Send(Command("editor-property","font",JsonValue.Create(font)));};properties.Children.Add(picker);}
  string group=kind switch{"orb"=>"Orbe","spectrum"=>"Espectro","card"=>"Tarjeta","clock"=>"Reloj","stars" or "mesh"=>"Fondo animado",_=>""};
  foreach(var spec in state["specs"]!.AsArray().OfType<JsonObject>().Where(s=>s["group"]!.GetValue<string>()==group)){
   string key=spec["key"]!.GetValue<string>(),title=spec["title"]!.GetValue<string>(),type=spec["kind"]!.GetValue<string>();var value=item["style"]?[key]??state["defaults"]?[key]??spec["value"];
   if(type=="slider")Range(title,key,value?.GetValue<double>()??0,spec["min"]!.GetValue<double>(),spec["max"]!.GetValue<double>(),spec["step"]!.GetValue<double>(),true);
   else if(type=="color")ColorInput(title,key,value?.GetValue<string>()??"#FFFFFF",true);
   else if(type is "bool" or "toggle"){bool on=value is JsonValue v&&(v.TryGetValue<bool>(out bool b)?b:v.TryGetValue<double>(out var n)&&n>.5);Toggle(title,on,key,true);}
  }
  Range("Opacidad","opacity",item["opacity"]!.GetValue<double>(),0,1,.01,false);
  var general=new StackPanel{Spacing=14};for(int i=0;i<generalCount;i++){var child=properties.Children[0];properties.Children.RemoveAt(0);general.Children.Add(child);}properties.Children.Add(new Expander{Header=L10n.Text("Propiedades del objeto"),Content=general,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
 }
 async Task Cancel(){if(closing||confirming)return;confirming=true;try{await Drain();state=await Request(Command("editor-get"));bool discard=true;if(state["dirty"]?.GetValue<bool>()==true){panel.Activate();var dialog=new ContentDialog{Title=L10n.Text("¿Descartar los cambios del diseño?"),Content=L10n.Text("Los cambios todavía no se han guardado."),PrimaryButtonText=L10n.Text("Descartar"),CloseButtonText=L10n.Text("Seguir editando"),DefaultButton=ContentDialogButton.Close,XamlRoot=panelRoot.XamlRoot};discard=await dialog.ShowAsync()==ContentDialogResult.Primary;}if(discard)await Send(Command("editor-discard"));else await Send(Command("editor-continue"));}catch(Exception ex){if(!HostAlive())CloseAll();else Error(ex.Message);}finally{confirming=false;}}
 async void Keys(object sender,KeyRoutedEventArgs e){
  bool control=Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
  if(e.Key==Windows.System.VirtualKey.Escape){e.Handled=true;await Cancel();return;}if(!control)return;
  if(e.Key==Windows.System.VirtualKey.S){e.Handled=true;await Drain();await Send(Command("editor-save"));}
  else if(e.OriginalSource is not TextBox&&e.Key is Windows.System.VirtualKey.Z or Windows.System.VirtualKey.Y){e.Handled=true;await Drain();await Send(Command(e.Key==Windows.System.VirtualKey.Z?"editor-undo":"editor-redo"));}
 }
 void ToggleProperties(){if(IsWindowVisible(WindowNative.GetWindowHandle(panel))){panel.AppWindow.Hide();WritePanelStatus();}else ShowProperties();}
 void ShowProperties(){panel.AppWindow.Show();SetWindowPos(WindowNative.GetWindowHandle(panel),new IntPtr(-1),0,0,0,0,0x0013);panel.Activate();WritePanelStatus();}
 void WritePanelStatus(){try{File.WriteAllText(IOPath.Combine(IOPath.GetTempPath(),"WirePulse-editor-state.json"),new JsonObject{["visible"]=IsWindowVisible(WindowNative.GetWindowHandle(panel)),["x"]=panel.AppWindow.Position.X,["y"]=panel.AppWindow.Position.Y,["width"]=panel.AppWindow.Size.Width,["height"]=panel.AppWindow.Size.Height,["selected"]=selected,["controls"]=properties.Children.Count,["error"]=error.IsOpen?error.Message:null}.ToJsonString());}catch{}}
 void CloseAll(){if(closing)return;closing=true;poll.Stop();flush.Stop();panel.Close();toolbar.Close();}
}
