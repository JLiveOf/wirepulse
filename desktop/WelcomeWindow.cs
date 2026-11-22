using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;

// A version is acknowledged only after the user saves the initial choices.
public sealed class WelcomeState(string? path = null)
{
 readonly string file = path ?? Path.Combine(Settings.Folder, "welcome.json");
 public bool Needs(string version) { try { return JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(file))?.GetValueOrDefault("completedVersion") != version; } catch { return true; } }
 public void Complete(string version) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);var temp=file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new {completedVersion=version}));File.Move(temp,file,true); }
}

public sealed class WelcomeWindow : Window
{
 readonly Canvas canvas=new(); readonly Image logo; readonly TextBlock name; readonly StackPanel form; readonly DispatcherTimer clock;
 readonly WelcomeSound sound=new(); readonly Bitmap bitmap; readonly Stopwatch elapsed=new(); bool transitioning,configured; double transitionAt;
 public WelcomeWindow(bool enabled,bool priority,bool automatic,string channel,Func<bool,bool,bool,string,string?> save,bool playSound=true)
 {
  Title="WirePulse";Width=620;Height=560;CanResize=false;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  Background=new SolidColorBrush(Color.Parse("#101115"));Icon=new WindowIcon(Path.Combine(AppContext.BaseDirectory,"Assets","WirePulse.png"));
  bitmap=new Bitmap(Path.Combine(AppContext.BaseDirectory,"Assets","WirePulse.png"));logo=new Image{Source=bitmap,Width=136,Height=136};name=new TextBlock{Text="WirePulse",FontSize=30,FontWeight=FontWeight.Bold};canvas.Children.Add(logo);canvas.Children.Add(name);
  var startup=new CheckBox{Content=L10n.Text("Abrir WirePulse al iniciar sesión"),IsChecked=enabled};
  var high=new CheckBox{Content=L10n.Text("Inicio prioritario (Windows)"),IsChecked=priority,IsVisible=OperatingSystem.IsWindows(),IsEnabled=enabled};startup.IsCheckedChanged+=(_,_)=>high.IsEnabled=startup.IsChecked==true;
  var updates=new CheckBox{Content=L10n.Text("Comprobar actualizaciones automáticamente"),IsChecked=automatic};
  var channels=new ComboBox{ItemsSource=new[]{L10n.Text("Estable"),L10n.Text("Beta (prereleases)")},SelectedIndex=channel=="beta"?1:0,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
  var error=new TextBlock{Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap};
  var finish=new Button{Content=L10n.Text("Empezar"),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};
  form=new StackPanel{Spacing=12,Width=548,Opacity=0,IsVisible=false};form.Children.Add(new TextBlock{Text=L10n.Text("Vamos a preparar tu escritorio"),FontSize=23,FontWeight=FontWeight.SemiBold});form.Children.Add(startup);form.Children.Add(high);form.Children.Add(updates);form.Children.Add(new TextBlock{Text=L10n.Text("Canal de actualizaciones")});form.Children.Add(channels);form.Children.Add(new TextBlock{Text=L10n.Text("Puedes cambiar estas opciones más adelante en Ajustes."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});form.Children.Add(error);form.Children.Add(finish);canvas.Children.Add(form);Canvas.SetLeft(form,36);Canvas.SetTop(form,106);
  finish.Click+=(_,_)=>{try{var message=save(startup.IsChecked==true,startup.IsChecked==true&&high.IsChecked==true,updates.IsChecked==true,channels.SelectedIndex==1?"beta":"stable");if(message!=null){error.Text=L10n.Text(message);return;}configured=true;Close();}catch(Exception e){error.Text=L10n.Text("No se pudieron guardar los ajustes iniciales: ")+e.Message;}};
  Content=canvas;clock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};clock.Tick+=(_,_)=>Animate();
  Opened+=(_,_)=>{PositionHeader(0);elapsed.Start();if(playSound)sound.Play();clock.Start();};Closed+=(_,_)=>{clock.Stop();sound.Dispose();bitmap.Dispose();};
 }
 public bool Configured=>configured;
 public void ShowSetup(bool animate=true){if(transitioning)return;transitioning=true;transitionAt=elapsed.Elapsed.TotalSeconds;form.IsVisible=true;sound.Dispose();if(!animate){PositionHeader(1);form.Opacity=1;clock.Stop();}}
 void Animate(){double t=elapsed.Elapsed.TotalSeconds;if(!transitioning&&t>=2.9)ShowSetup();double progress=transitioning?Math.Clamp((t-transitionAt)/.55,0,1):0;double eased=1-Math.Pow(1-progress,3);PositionHeader(eased);form.Opacity=eased;if(progress>=1)clock.Stop();}
 void PositionHeader(double t){double w=ClientSize.Width>0?ClientSize.Width:620;logo.Width=logo.Height=136+(42-136)*t;Canvas.SetLeft(logo,(w-136)/2+(28-(w-136)/2)*t);Canvas.SetTop(logo,140+(24-140)*t);name.FontSize=30+(22-30)*t;Canvas.SetLeft(name,(w-144)/2+(84-(w-144)/2)*t);Canvas.SetTop(name,292+(31-292)*t);}
}

sealed class WelcomeSound : IDisposable
{
#if WINDOWS
 NAudio.Wave.WaveOutEvent? output; NAudio.Wave.WaveFileReader? source;
#else
 Process? player;
#endif
 public void Play(){try{
  string file=Path.Combine(AppContext.BaseDirectory,"Assets","Welcome.wav");
#if WINDOWS
  source=new(file);output=new();output.Init(source);output.Play();
#else
  player=new Process{StartInfo=new ProcessStartInfo("paplay"){UseShellExecute=false,RedirectStandardError=true}};player.StartInfo.ArgumentList.Add(file);player.Start();_=player.StandardError.ReadToEndAsync();
#endif
 }catch(Exception e){Trace.WriteLine("Welcome audio: "+e.Message);Dispose();}}
 public void Dispose(){
#if WINDOWS
  try{output?.Stop();}catch(Exception e){Trace.WriteLine(e);}finally{output?.Dispose();output=null;source?.Dispose();source=null;}
#else
  try{if(player is {HasExited:false})player.Kill();}catch(InvalidOperationException){}player?.Dispose();player=null;
#endif
 }
}
