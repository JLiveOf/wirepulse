using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
namespace WirePulse;

public sealed class SettingsWindow:Window {
 Settings current;readonly Settings global;readonly Dictionary<string,Settings> displays;
 readonly StackPanel content=new(){Spacing=15};readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
 readonly ComboBox groups=new(){ItemsSource=new[]{"Audio","Fondo","Orbe","Espectro","Tarjeta","Rendimiento"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,15)};
 readonly ComboBox selector=new(){HorizontalAlignment=HorizontalAlignment.Stretch};readonly CheckBox mode=new(){Content="Ajustar cada pantalla de forma independiente"};
 readonly MediaService media;readonly Spectrum spectrum;readonly Func<string> audio;readonly DispatcherTimer timer;
 public SettingsWindow(Settings settings,Spectrum spectrum,MediaService media,Func<string> audio,Dictionary<string,Settings> displays){
  global=settings;current=settings;this.displays=displays;this.spectrum=spectrum;this.media=media;this.audio=audio;
  Title="WirePulse · Ajustes";Width=780;Height=700;MinWidth=600;MinHeight=450;Icon=new WindowIcon(Path.Combine(AppContext.BaseDirectory,"Assets","WirePulse.png"));
  var root=new DockPanel{Margin=new Thickness(20)};DockPanel.SetDock(groups,Dock.Top);root.Children.Add(groups);
  var screens=new StackPanel{Spacing=8,Margin=new Thickness(0,0,0,15)};screens.Children.Add(mode);screens.Children.Add(selector);DockPanel.SetDock(screens,Dock.Top);root.Children.Add(screens);
  var reset=new Button{Content="Restablecer diseño",Margin=new Thickness(0,15,0,0)};DockPanel.SetDock(reset,Dock.Bottom);reset.Click+=(_,_)=>{current.Reset();ShowGroup();};root.Children.Add(reset);
  root.Children.Add(new ScrollViewer{Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled});Content=root;
  groups.SelectionChanged+=(_,_)=>ShowGroup();selector.SelectionChanged+=(_,_)=>{current=global.IndependentDisplays&&selector.SelectedItem is string key&&displays.TryGetValue(key,out var profile)?profile:global;ShowGroup();};
  mode.IsChecked=global.IndependentDisplays;mode.IsCheckedChanged+=(_,_)=>{global.IndependentDisplays=mode.IsChecked==true;global.Save();RefreshDisplays();};RefreshDisplays();
  timer=new DispatcherTimer(TimeSpan.FromSeconds(1),DispatcherPriority.Background,(_,_)=>Refresh());timer.Start();Closed+=(_,_)=>timer.Stop();
 }
 public void RefreshDisplays(){var selected=selector.SelectedItem as string;var names=global.IndependentDisplays?displays.Keys.ToArray():new[]{"Todas las pantallas"};selector.ItemsSource=names;selector.IsEnabled=global.IndependentDisplays;selector.SelectedItem=names.Contains(selected)?selected:names.FirstOrDefault();current=global.IndependentDisplays&&selector.SelectedItem is string key&&displays.TryGetValue(key,out var profile)?profile:global;ShowGroup();}
 void Refresh(){var s=spectrum.Snapshot();status.Text=$"{audio()}\n64 bandas · {s.SampleRate:N0} Hz · FFT {s.FftSize} · {(s.FftSize>0?s.SampleRate/(double)s.FftSize:0):F2} Hz/bin\n{media.Status}";}
 void Label(string text)=>content.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap});
 void Toggle(string label,bool value,Action<bool> change){var check=new CheckBox{Content=label,IsChecked=value};check.IsCheckedChanged+=(_,_)=>change(check.IsChecked==true);content.Children.Add(check);}
 void Input(string label,string value,Action<string> change,bool secret=false){Label(label);var box=new TextBox{Text=value,PasswordChar=secret?'●':'\0'};var saved=value;box.LostFocus+=(_,_)=>{var next=box.Text??"";if(next!=saved){saved=next;change(next);}};content.Children.Add(box);}
 void ShowGroup(){
  var target=current;var group=groups.SelectedItem as string??"Audio";content.Children.Clear();
  if(group=="Audio"){Refresh();content.Children.Add(status);Label("Captura la salida del sistema en memoria. No usa el micrófono ni guarda audio. El espectro cubre desde DC hasta Nyquist; la resolución depende del muestreo real de salida.");}
  if(group=="Tarjeta"){
   Label("Proveedor de música");var choice=new ComboBox{ItemsSource=new[]{"Automático","Apple Music","Spotify","Cider"},SelectedItem=global.MediaSource,HorizontalAlignment=HorizontalAlignment.Stretch};choice.SelectionChanged+=(_,_)=>{global.MediaSource=choice.SelectedItem?.ToString()??"Automático";global.Save();};content.Children.Add(choice);
   Toggle("Tarjeta arriba centrada",target.TopCentered,b=>{target.TopCentered=b;target.Save();});Input("Margen superior (%)",target.TopMargin.ToString(System.Globalization.CultureInfo.InvariantCulture),s=>{if(double.TryParse(s,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)){target.TopMargin=Math.Clamp(n,0,80);target.Save();}});
   Label("Cider: activa su API local y crea un token de Playback en Ajustes → Connectivity. Si publica los controles multimedia del sistema, se detecta también sin API.");Input("Dirección local de Cider",global.CiderUrl,s=>{if(Uri.TryCreate(s,UriKind.Absolute,out var u)&&u.IsLoopback&&u.Scheme=="http"){if(s!=global.CiderUrl)global.CiderToken="";global.CiderUrl=s;global.Save();}});Input("Token de Cider",global.CiderToken,s=>{global.CiderToken=s;global.Save();},true);
  }
  foreach(var spec in target.Specs.Where(x=>x.Group==group&&x.Key!="quality")){
   if(spec.Kind is "toggle" or "bool")Toggle(spec.Title,target.On(spec.Key),b=>target.Set(spec.Key,b));
   else if(spec.Kind=="slider"){var caption=new TextBlock{Text=$"{spec.Title}: {target.N(spec.Key):0.###}"};content.Children.Add(caption);var slider=new Slider{Minimum=spec.Min,Maximum=spec.Max,Value=target.N(spec.Key),TickFrequency=spec.Step,IsSnapToTickEnabled=true};slider.PropertyChanged+=(_,e)=>{if(e.Property==Slider.ValueProperty){caption.Text=$"{spec.Title}: {slider.Value:0.###}";target.Set(spec.Key,slider.Value);}};content.Children.Add(slider);}
   else Input(spec.Title,target.Text(spec.Key),s=>{if(spec.Kind=="color"&&!Color.TryParse(s,out _))return;target.Set(spec.Key,s);});
  }
  if(group=="Rendimiento"){Toggle("Pausar animación",target.Paused,b=>target.Paused=b);Label("Dibujo a la resolución nativa de cada pantalla. La sincronización de la pantalla puede limitar los FPS.");}
 }
}
