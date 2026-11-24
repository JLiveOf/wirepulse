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
 sealed record Preferences(string CompletedVersion="",bool ShowOnStartup=false,string SeenNewsVersion="");
 Preferences Read(){try{return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new();}catch{return new();}}
 void Write(Preferences value){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);var temp=file+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(value,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));File.Move(temp,file,true);}
 public bool Needs(string version)=>Read().CompletedVersion!=version;
 public bool NeedsSetup(bool startupAsked=false)=>string.IsNullOrEmpty(Read().CompletedVersion)&&!startupAsked;
 public bool NeedsNews(string version)=>Read().SeenNewsVersion!=version;
 public void CompleteNews(string version)=>Write(Read() with{SeenNewsVersion=version});
 public bool ShowOnStartup{get=>Read().ShowOnStartup;set=>Write(Read() with{ShowOnStartup=value});}
 public void Complete(string version)=>Write(Read() with{CompletedVersion=version});
}

public enum WelcomePhase{Intro,Setup,News}
public sealed class WelcomeWindow : Window
{
 public const double CompactWidth=320,CompactHeight=280,SetupWidth=620,SetupHeight=640;
 readonly Canvas canvas=new(){ClipToBounds=true};readonly Image logo;readonly TextBlock name;readonly StackPanel form;readonly TextBlock error=new(){Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap};readonly DispatcherTimer clock;
 readonly WelcomeSound sound=new();readonly SetupMusic music=new();readonly Bitmap bitmap;readonly Stopwatch elapsed=new();readonly bool initialSetup,showNews,audioEnabled;readonly Action<bool>? saveSplash;readonly Action? acknowledgeNews;readonly Func<bool,bool,bool,string,string?> save;
 readonly Button close=new(){Content="×",Width=32,Height=32,IsVisible=false};readonly Image backdrop=new(){Width=SetupWidth,Height=SetupHeight,Opacity=.75,IsHitTestVisible=false};Bitmap? backdropBitmap;
 bool transitioning,configured,expanded,fadeOnly,finishing,allowClose;readonly double introNameWidth;double transitionAt;Point center;double scale=1;
 readonly CheckBox startup,high,updates,splashToggle;readonly ComboBox channels;
 public static double IntroDuration{get;}=ReadDuration();
 static double ReadDuration(){try{using var reader=new BinaryReader(File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Assets","Welcome.wav")));reader.ReadBytes(12);int bytesPerSecond=176400;while(reader.BaseStream.Position+8<=reader.BaseStream.Length){string id=System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));int size=reader.ReadInt32();long next=reader.BaseStream.Position+size+(size&1);if(id=="fmt "){reader.ReadBytes(8);bytesPerSecond=reader.ReadInt32();}if(id=="data")return(double)size/Math.Max(1,bytesPerSecond);reader.BaseStream.Position=next;}}catch{}return 6.136;}
 public WelcomeWindow(bool enabled,bool priority,bool automatic,string channel,Func<bool,bool,bool,string,string?> save,bool playSound=true,bool initialSetup=true,bool showOnStartup=false,Action<bool>? saveSplash=null,bool showNews=false,Action? acknowledgeNews=null,bool skipIntro=false)
 {
  this.initialSetup=initialSetup;this.showNews=showNews;this.saveSplash=saveSplash;this.acknowledgeNews=acknowledgeNews;this.save=save;audioEnabled=playSound;
  Topmost=true;Title="WirePulse";Width=CompactWidth;Height=CompactHeight;CanResize=false;WindowDecorations=WindowDecorations.None;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  TransparencyLevelHint=[WindowTransparencyLevel.AcrylicBlur,WindowTransparencyLevel.Blur,WindowTransparencyLevel.Transparent];TransparencyBackgroundFallback=new SolidColorBrush(Color.Parse("#242730"));Background=Brushes.Transparent;
  Icon=new WindowIcon(Path.Combine(AppContext.BaseDirectory,"Assets","WirePulse.png"));bitmap=new Bitmap(Path.Combine(AppContext.BaseDirectory,"Assets","WirePulse.png"));
  canvas.Children.Add(backdrop);logo=new Image{Source=bitmap,Width=112,Height=112};name=new TextBlock{Text="WirePulse",FontSize=30,FontWeight=FontWeight.Bold,Foreground=Brushes.White,Width=190,FontFamily=new FontFamily("Inter"),TextAlignment=TextAlignment.Left};introNameWidth=new FormattedText("WirePulse",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Inter",FontStyle.Normal,FontWeight.Bold),30,Brushes.White).Width;canvas.Children.Add(logo);canvas.Children.Add(name);
  startup=new CheckBox{Content=L10n.Text("Abrir WirePulse al iniciar sesión"),IsChecked=enabled};high=new CheckBox{Content=L10n.Text("Inicio prioritario (Windows)"),IsChecked=priority,IsVisible=OperatingSystem.IsWindows(),IsEnabled=enabled};startup.IsCheckedChanged+=(_,_)=>high.IsEnabled=startup.IsChecked==true;
  updates=new CheckBox{Content=L10n.Text("Comprobar actualizaciones automáticamente"),IsChecked=automatic};splashToggle=new CheckBox{Content=L10n.Text("Mostrar la splash en cada inicio"),IsChecked=showOnStartup};channels=new ComboBox{ItemsSource=new[]{L10n.Text("Estable"),L10n.Text("Beta (prereleases)")},SelectedIndex=channel=="beta"?1:0,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
  form=new StackPanel{Spacing=12,Width=548,Opacity=0,IsVisible=false};canvas.Children.Add(form);canvas.Children.Add(close);Canvas.SetLeft(form,36);Canvas.SetTop(form,106);
  close.Click+=(_,_)=>Close();KeyDown+=(_,e)=>{if(e.Key==Avalonia.Input.Key.Escape)Close();};canvas.PointerPressed+=(_,e)=>{if(e.Source==canvas&&e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};
  Closing+=(_,e)=>{if(!allowClose&&Phase!=WelcomePhase.Intro){e.Cancel=true;if(!finishing&&Phase==WelcomePhase.News){acknowledgeNews?.Invoke();NewsAcknowledged=true;configured=true;}Dispatcher.UIThread.Post(async()=>await Finish());}};
  Content=new Border{CornerRadius=new CornerRadius(28),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.Parse("#50ffffff")),Background=new SolidColorBrush(Color.Parse("#55202630")),ClipToBounds=true,Child=canvas};clock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};clock.Tick+=(_,_)=>Animate();
  var screen=Screens.Primary??Screens.All.FirstOrDefault();if(screen!=null&&OperatingSystem.IsLinux()){backdropBitmap=WelcomeBackdrop.Capture(screen,SetupWidth,SetupHeight);backdrop.Source=backdropBitmap;}
  Opened+=(_,_)=>{scale=Screens.ScreenFromWindow(this)?.Scaling??1;center=new Point(Position.X+CompactWidth*scale/2,Position.Y+CompactHeight*scale/2);PositionHeader(0);SetIntroProgress(0);elapsed.Start();if(skipIntro)ShowNews(false);else{if(playSound)sound.Play();clock.Start();}};Closed+=(_,_)=>{clock.Stop();sound.Dispose();music.Dispose();bitmap.Dispose();backdropBitmap?.Dispose();};
 }
 public WelcomePhase Phase{get;private set;}=WelcomePhase.Intro;
 public bool Configured=>configured;
 public bool NewsAcknowledged{get;private set;}
 public void SetIntroProgress(double progress){double front=Math.Clamp(progress,0,1)*1.4-.2;if(front<=0){name.Opacity=0;return;}name.Opacity=1;if(front>=1.2){name.OpacityMask=null;return;}name.OpacityMask=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative),GradientStops=new GradientStops{new GradientStop(Colors.White,0),new GradientStop(Colors.White,Math.Clamp(front-.2,0,1)),new GradientStop(Colors.Transparent,Math.Clamp(front+.2,0,1)),new GradientStop(Colors.Transparent,1)}};}
 Button MusicButton(){var mute=new Button{Content=L10n.Text(music.Muted?"Activar música":"Silenciar música")};mute.Click+=(_,_)=>{music.Muted=!music.Muted;mute.Content=L10n.Text(music.Muted?"Activar música":"Silenciar música");};return mute;}
 void BuildSetup(){form.Children.Clear();form.Spacing=12;form.Children.Add(new TextBlock{Text=L10n.Text("Vamos a preparar tu escritorio"),FontSize=23,FontWeight=FontWeight.SemiBold});foreach(var control in new Control[]{startup,high,updates,splashToggle,new TextBlock{Text=L10n.Text("Canal de actualizaciones")},channels,new TextBlock{Text=L10n.Text("Puedes cambiar estas opciones más adelante en Ajustes."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray},error})form.Children.Add(control);
  var finish=new Button{Content=L10n.Text(showNews?"Continuar":"Empezar")};finish.Click+=async(_,_)=>{try{var message=save(startup.IsChecked==true,startup.IsChecked==true&&high.IsChecked==true,updates.IsChecked==true,channels.SelectedIndex==1?"beta":"stable");if(message!=null){error.Text=L10n.Text(message);return;}saveSplash?.Invoke(splashToggle.IsChecked==true);configured=true;if(showNews)ShowNews();else await Finish();}catch(Exception e){error.Text=L10n.Text("No se pudieron guardar los ajustes iniciales: ")+e.Message;}};form.Children.Add(new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=12,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right,Children={MusicButton(),finish}});
 }
 void BuildNews(){form.Children.Clear();form.Spacing=18;error.Text="";form.Children.Add(new TextBlock{Text=L10n.Text("Novedades de\nWirePulse"),FontSize=32,FontWeight=FontWeight.Bold,TextAlignment=TextAlignment.Center,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch,Foreground=Brushes.White});form.Children.Add(new TextBlock{Text=UpdateService.InstalledVersion,FontSize=13,Foreground=new SolidColorBrush(Color.Parse("#ff5b80")),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center});
  var rows=new StackPanel{Spacing=22};foreach(var feature in WhatsNewContent.Current.Features){string path=feature.Icon switch{"music"=>"M9,18 L9,5 L20,3 L20,16 M9,8 L20,6 M9,18 C9,22 2,23 2,19 C2,15 9,15 9,18 M20,16 C20,20 13,21 13,17 C13,13 20,13 20,16", "update"=>"M20,8 A9,9 0 1,0 21,15 M20,2 L20,8 L14,8 M8,12 L11,15 L17,9",_=>"M12,1 L15,9 L23,12 L15,15 L12,23 L9,15 L1,12 L9,9 Z"};var icon=new Avalonia.Controls.Shapes.Path{Data=StreamGeometry.Parse(path),Width=32,Height=32,Stretch=Stretch.Uniform,StrokeThickness=1.8,Fill=Brushes.Transparent,Stroke=new SolidColorBrush(Color.Parse("#ff2d55")),VerticalAlignment=Avalonia.Layout.VerticalAlignment.Top,Margin=new Thickness(0,6,0,0)};var row=new Grid{ColumnDefinitions=new ColumnDefinitions("48,*")};row.Children.Add(icon);var copy=new StackPanel{Spacing=5,Children={new TextBlock{Text=L10n.Text(feature.Title),FontSize=18,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap},new TextBlock{Text=L10n.Text(feature.Description),FontSize=14,TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.Parse("#c5c7cf"))}}};Grid.SetColumn(copy,1);row.Children.Add(copy);rows.Children.Add(row);}form.Children.Add(new ScrollViewer{MaxHeight=320,Content=rows});form.Children.Add(error);
  var finish=new Button{Content=L10n.Text("Continuar"),Background=new SolidColorBrush(Color.Parse("#df2558")),Foreground=Brushes.White};finish.Click+=async(_,_)=>{try{acknowledgeNews?.Invoke();NewsAcknowledged=true;configured=true;await Finish();}catch(Exception e){error.Text=L10n.Text("No se pudieron guardar las novedades vistas: ")+e.Message;}};form.Children.Add(new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right,Spacing=12,Children={MusicButton(),finish}});
 }
 async Task Finish(){if(finishing)return;finishing=true;form.IsEnabled=false;music.Dispose();if(audioEnabled){using var effect=new WelcomeSound();effect.Play("Shutdown.wav");await Task.Delay(600);}allowClose=true;Close();}
 void Expand(bool animate){sound.Dispose();if(audioEnabled)music.Start();form.IsVisible=true;close.IsVisible=true;name.Opacity=1;name.OpacityMask=null;fadeOnly=expanded;if(!expanded){center=new Point(Position.X+Width*scale/2,Position.Y+Height*scale/2);expanded=true;}transitionAt=elapsed.Elapsed.TotalSeconds;transitioning=animate;form.Opacity=animate?0:1;if(!animate){PositionHeader(1);clock.Stop();}else clock.Start();}
 public void ShowSetup(bool animate=true){if(Phase!=WelcomePhase.Intro)return;if(initialSetup){Phase=WelcomePhase.Setup;BuildSetup();Expand(animate);}else if(showNews)ShowNews(animate);else Close();}
 public void ShowNews(bool animate=true){if(Phase==WelcomePhase.News)return;Phase=WelcomePhase.News;BuildNews();Expand(animate);}
 void Animate(){double t=elapsed.Elapsed.TotalSeconds;if(Phase==WelcomePhase.Intro){SetIntroProgress(t/IntroDuration);if(t>=IntroDuration+.15&&(!sound.IsPlaying||t>=IntroDuration+4))ShowSetup();}if(!transitioning)return;double progress=Math.Clamp((t-transitionAt)/.8,0,1),eased=1-Math.Pow(1-progress,3);if(!fadeOnly)PositionHeader(eased);form.Opacity=Math.Clamp((eased-.25)/.75,0,1);if(progress>=1){transitioning=false;clock.Stop();}}
 void PositionHeader(double t){double w=CompactWidth+(SetupWidth-CompactWidth)*t,h=CompactHeight+(SetupHeight-CompactHeight)*t;Width=w;Height=h;if(IsVisible)Position=new PixelPoint((int)Math.Round(center.X-w*scale/2),(int)Math.Round(center.Y-h*scale/2));logo.Width=logo.Height=112+(42-112)*t;Canvas.SetLeft(logo,104+(28-104)*t);Canvas.SetTop(logo,42+(24-42)*t);name.FontSize=30+(22-30)*t;Canvas.SetLeft(name,(CompactWidth-introNameWidth)/2+(84-(CompactWidth-introNameWidth)/2)*t);Canvas.SetTop(name,174+(31-174)*t);Canvas.SetLeft(close,w-48);Canvas.SetTop(close,24);Canvas.SetLeft(backdrop,(w-SetupWidth)/2);Canvas.SetTop(backdrop,(h-SetupHeight)/2);}
}

sealed class WelcomeSound : IDisposable
{
#if WINDOWS
 NAudio.Wave.WaveOutEvent? output; NAudio.Wave.WaveFileReader? source;
#else
 Process? player;
#endif
 public bool IsPlaying{get{try{
#if WINDOWS
  return output?.PlaybackState==NAudio.Wave.PlaybackState.Playing;
#else
  return player is {HasExited:false};
#endif
 }catch{return false;}}}
 public void Play(string asset="Welcome.wav"){try{
  string file=Path.Combine(AppContext.BaseDirectory,"Assets",asset);
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
