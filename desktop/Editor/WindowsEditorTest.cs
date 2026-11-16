#if WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;

public sealed partial class App {
 void RunWindowsEditorTest(IClassicDesktopStyleApplicationLifetime life,string output){
  settings=new Settings("native-editor-test-"+Guid.NewGuid()).Preview();media=new MediaService(settings);audio=new AudioCapture(spectrum);screenProbe=new Window();
  var screen=screenProbe.Screens.All.First();var size=new Size(screen.Bounds.Width/screen.Scaling,screen.Bounds.Height/screen.Scaling);
  var orb=SceneDocument.New("orb",size,settings);orb.Style["density"]=JsonValue.Create(24d);orb.X=.2;orb.Y=.2;orb.Width=.3;orb.Height=.4;
  var text=SceneDocument.New("text",size,settings);text.Text="Prueba de editor nativo";
  settings.Scene=new SceneDocument{Items=[orb,text]};string original=JsonSerializer.Serialize(settings.Scene);bool discard=Program.Args.Contains("--editor-test-discard");
  editor=new EditorWindow(settings,spectrum,media,screen.Bounds,screen.Scaling,"Prueba nativa",orb.Id);
  var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(30)};bool completed=false;
  void Write(bool valid,string? error){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{valid,discard,error,display=screen.DisplayName,sceneItems=settings.Scene?.Items.Count},new JsonSerializerOptions{WriteIndented=true}));}
  editor.Closed+=(_,_)=>{
   completed=true;timeout.Stop();editor=null;bool valid=false;string? failure=null;
   try{var ui=JsonNode.Parse(File.ReadAllText(output+".ui.json"))!;valid=ui["valid"]!.GetValue<bool>();if(!valid)failure=ui["error"]?.GetValue<string>();
    if(discard)valid&=JsonSerializer.Serialize(settings.Scene)==original;
    else{var saved=settings.Scene!.Items.Single(i=>i.Id==orb.Id);valid&=saved.Style["density"]!.GetValue<double>()==27&&Math.Abs(saved.Opacity-.64)<.001&&settings.Scene.Items.Last().Id==orb.Id;}
    if(!valid&&failure==null)failure="El resultado guardado/descartado no coincide con el borrador del editor";
   }catch(Exception ex){failure=ex.ToString();}
   Write(valid,failure);var finish=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};finish.Tick+=(_,_)=>{finish.Stop();life.Shutdown(valid?0:1);};finish.Start();
  };
  timeout.Tick+=(_,_)=>{if(completed)return;timeout.Stop();Write(false,"Native editor test timed out");life.Shutdown(1);};
  life.Exit+=(_,_)=>{timeout.Stop();windowsSettings?.Dispose();editor?.Close();audio.Dispose();screenProbe?.Close();};
  editor.Show();editor.Activate();ShowWindowsEditor();timeout.Start();
 }
}
#endif
