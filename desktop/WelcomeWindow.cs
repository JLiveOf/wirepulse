using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;

public sealed class WelcomeState(string? path = null)
{
 readonly string file = path ?? Path.Combine(Settings.Folder, "welcome.json");
 sealed record Preferences(string CompletedVersion="",bool ShowOnStartup=false);
 Preferences Read(){try{return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new();}catch{return new();}}
 void Write(Preferences value){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);var temp=file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(value,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));File.Move(temp,file,true);}
 public bool Needs(string version)=>Read().CompletedVersion!=version;
 public bool ShowOnStartup{get=>Read().ShowOnStartup;set=>Write(Read() with{ShowOnStartup=value});}
 public void Complete(string version)=>Write(Read() with{CompletedVersion=version});
}

public sealed class WelcomeWindow : Window
{
 public const double CompactWidth=320,CompactHeight=280,SetupWidth=620,SetupHeight=640;
 readonly Canvas canvas=new(){ClipToBounds=true};readonly Image logo;readonly TextBlock name;readonly StackPanel form;readonly DispatcherTimer clock;
 readonly WelcomeSound sound=new();readonly Bitmap bitmap;readonly Stopwatch elapsed=new();readonly bool initialSetup;readonly CheckBox splashToggle;readonly Action<bool>? saveSplash;
 readonly Button close=new(){Content="×",Width=32,Height=32,IsVisible=false};readonly Image backdrop=new(){Width=SetupWidth,Height=SetupHeight,Opacity=.75,IsHitTestVisible=false};Bitmap? backdropBitmap;
 bool transitioning,configured;readonly double introNameWidth;double transitionAt;Point center;double scale=1;
 public static double IntroDuration{get;}=ReadDuration();
 static double ReadDuration(){try{using var reader=new BinaryReader(File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Assets","Welcome.wav")));
 reader.ReadBytes(12);int bytesPerSecond=176400;while(reader.BaseStream.Position+8<=reader.BaseStream.Length){string id=System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));int size=reader.ReadInt32();long next=reader.BaseStream.Position+size+(size&1);if(id=="fmt "){reader.ReadBytes(8);bytesPerSecond=reader.ReadInt32();}if(id=="data")return (double)size/Math.Max(1,bytesPerSecond);reader.BaseStream.Position=next;}}catch{}return 6.136;}
 public WelcomeWindow(bool enabled,bool priority,bool automatic,string channel,Func<bool,bool,bool,string,string?> save,bool playSound=true,bool initialSetup=true,bool showOnStartup=false,Action<bool>? saveSplash=null)
 {
  this.initialSetup=initialSetup;this.saveSplash=saveSplash;
  Title="WirePulse";Width=CompactWidth;Height=CompactHeight;CanResize=false;WindowDecorations=WindowDecorations.None;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  TransparencyLevelHint=[WindowTransparencyLevel.AcrylicBlur,WindowTransparencyLevel.Blur,WindowTransparencyLevel.Transparent];TransparencyBackgroundFallback=new SolidColorBrush(Color.Parse("#242730"));Background=Brushes.Transparent;
  Icon=new WindowIcon(Path.Combine(AppContext.BaseDirectory,"Assets","WirePulse.png"));bitmap=new Bitmap(Path.Combine(AppContext.BaseDirectory,"Assets","WirePulse.png"));
  canvas.Children.Add(backdrop);logo=new Image{Source=bitmap,Width=112,Height=112};name=new TextBlock{Text="WirePulse",FontSize=30,FontWeight=FontWeight.Bold,Foreground=Brushes.White,Width=190,FontFamily=new FontFamily("Inter"),TextAlignment=TextAlignment.Left};introNameWidth=new FormattedText("WirePulse",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Inter",FontStyle.Normal,FontWeight.Bold),30,Brushes.White).Width;canvas.Children.Add(logo);canvas.Children.Add(name);
  var startup=new CheckBox{Content=L10n.Text("Abrir WirePulse al iniciar sesión"),IsChecked=enabled};
  var high=new CheckBox{Content=L10n.Text("Inicio prioritario (Windows)"),IsChecked=priority,IsVisible=OperatingSystem.IsWindows(),IsEnabled=enabled};startup.IsCheckedChanged+=(_,_)=>high.IsEnabled=startup.IsChecked==true;
  var updates=new CheckBox{Content=L10n.Text("Comprobar actualizaciones automáticamente"),IsChecked=automatic};splashToggle=new CheckBox{Content=L10n.Text("Mostrar la splash en cada inicio"),IsChecked=showOnStartup};
  var channels=new ComboBox{ItemsSource=new[]{L10n.Text("Estable"),L10n.Text("Beta (prereleases)")},SelectedIndex=channel=="beta"?1:0,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
  var error=new TextBlock{Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap};var finish=new Button{Content=L10n.Text("Empezar"),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};
  form=new StackPanel{Spacing=12,Width=548,Opacity=0,IsVisible=false};form.Children.Add(new TextBlock{Text=L10n.Text("Vamos a preparar tu escritorio"),FontSize=23,FontWeight=FontWeight.SemiBold});form.Children.Add(startup);form.Children.Add(high);form.Children.Add(updates);form.Children.Add(splashToggle);form.Children.Add(new TextBlock{Text=L10n.Text("Canal de actualizaciones")});form.Children.Add(channels);form.Children.Add(new TextBlock{Text=L10n.Text("Puedes cambiar estas opciones más adelante en Ajustes."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});form.Children.Add(error);form.Children.Add(finish);canvas.Children.Add(form);canvas.Children.Add(close);Canvas.SetLeft(form,36);Canvas.SetTop(form,106);
  finish.Click+=(_,_)=>{try{var message=save(startup.IsChecked==true,startup.IsChecked==true&&high.IsChecked==true,updates.IsChecked==true,channels.SelectedIndex==1?"beta":"stable");if(message!=null){error.Text=L10n.Text(message);return;}this.saveSplash?.Invoke(splashToggle.IsChecked==true);configured=true;Close();}catch(Exception e){error.Text=L10n.Text("No se pudieron guardar los ajustes iniciales: ")+e.Message;}};
  close.Click+=(_,_)=>Close();KeyDown+=(_,e)=>{if(e.Key==Avalonia.Input.Key.Escape)Close();};canvas.PointerPressed+=(_,e)=>{if(e.Source==canvas&&e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};
  Content=new Border{CornerRadius=new CornerRadius(28),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.Parse("#50ffffff")),Background=new SolidColorBrush(Color.Parse("#55202630")),ClipToBounds=true,Child=canvas};clock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};clock.Tick+=(_,_)=>Animate();
  var screen=Screens.Primary;if(screen!=null&&OperatingSystem.IsLinux()){backdropBitmap=WelcomeBackdrop.Capture(screen,SetupWidth,SetupHeight);backdrop.Source=backdropBitmap;}
  Opened+=(_,_)=>{scale=Screens.ScreenFromWindow(this)?.Scaling??1;center=new Point(Position.X+CompactWidth*scale/2,Position.Y+CompactHeight*scale/2);PositionHeader(0);SetIntroProgress(0);if(playSound)sound.Play();elapsed.Start();clock.Start();};Closed+=(_,_)=>{clock.Stop();sound.Dispose();bitmap.Dispose();backdropBitmap?.Dispose();};
 }
 public bool Configured=>configured;
 public void SetIntroProgress(double progress){double front=Math.Clamp(progress,0,1)*1.4-.2;if(front<=0){name.Opacity=0;return;}name.Opacity=1;if(front>=1.2){name.OpacityMask=null;return;}name.OpacityMask=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative),GradientStops=new GradientStops{new GradientStop(Colors.White,0),new GradientStop(Colors.White,Math.Clamp(front-.2,0,1)),new GradientStop(Colors.Transparent,Math.Clamp(front+.2,0,1)),new GradientStop(Colors.Transparent,1)}};}
 public void ShowSetup(bool animate=true){if(transitioning)return;if(!initialSetup){Close();return;}transitioning=true;transitionAt=elapsed.Elapsed.TotalSeconds;form.IsVisible=true;close.IsVisible=true;sound.Dispose();name.Opacity=1;name.OpacityMask=null;if(!animate){PositionHeader(1);form.Opacity=1;clock.Stop();}}
 void Animate(){double t=elapsed.Elapsed.TotalSeconds;if(!transitioning){SetIntroProgress(t/IntroDuration);if(t>=IntroDuration+.15)ShowSetup();}if(!transitioning)return;double progress=Math.Clamp((t-transitionAt)/.8,0,1);double eased=1-Math.Pow(1-progress,3);PositionHeader(eased);form.Opacity=Math.Clamp((eased-.25)/.75,0,1);if(progress>=1)clock.Stop();}
 void PositionHeader(double t){double w=CompactWidth+(SetupWidth-CompactWidth)*t,h=CompactHeight+(SetupHeight-CompactHeight)*t;Width=w;Height=h;if(IsVisible)Position=new PixelPoint((int)Math.Round(center.X-w*scale/2),(int)Math.Round(center.Y-h*scale/2));logo.Width=logo.Height=112+(42-112)*t;Canvas.SetLeft(logo,104+(28-104)*t);Canvas.SetTop(logo,42+(24-42)*t);name.FontSize=30+(22-30)*t;Canvas.SetLeft(name,(CompactWidth-introNameWidth)/2+(84-(CompactWidth-introNameWidth)/2)*t);Canvas.SetTop(name,174+(31-174)*t);Canvas.SetLeft(close,w-48);Canvas.SetTop(close,24);Canvas.SetLeft(backdrop,(w-SetupWidth)/2);Canvas.SetTop(backdrop,(h-SetupHeight)/2);}
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
