using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse.WindowsSettings;

sealed partial class SettingsWindow {
 static IEnumerable<T> Descendants<T>(DependencyObject parent)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T match)yield return match;foreach(var descendant in Descendants<T>(child))yield return descendant;}}
 async Task RunSmokeTest(string output){
  var checks=new List<string>();var before=state.DeepClone().AsObject();string? failure=null;string[] names=before["displays"]!.AsObject().Select(p=>p.Key).ToArray();double original=0;string first=names.FirstOrDefault()??"";bool changed=false;
  try{
   foreach(var item in navigation.MenuItems.Cast<NavigationViewItem>()){navigation.SelectedItem=item;await Task.Delay(40);if(content.Children.Count<2||heading.Text!=L10n.Text((string)item.Tag))throw new Exception("Página incompleta: "+item.Tag);checks.Add("Native WinUI page: "+item.Tag);}
   if(names.Length==0)throw new Exception("Windows did not expose a display");
   state=await Request(Command("property","IndependentDisplays",JsonValue.Create(true),true));SyncDisplays();profile=first;displays.SelectedItem=first;navigation.SelectedItem=navigation.MenuItems[0];BuildPage();await Task.Delay(80);
   original=state["displays"]![first]!["values"]!["sensitivity"]!.GetValue<double>();changed=true;
   var slider=Descendants<Slider>(content).First(s=>Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(s)==L10n.Text("Sensibilidad de audio"));
   double testValue=Math.Abs(original-.91)>.001?.91:.73;slider.Value=testValue;await Task.Delay(300);state=await Request(Command("get"));
   if(Math.Abs(state["displays"]![first]!["values"]!["sensitivity"]!.GetValue<double>()-testValue)>.0001)throw new Exception("Native slider did not reach the renderer");
   foreach(string name in names.Skip(1)){if(state["displays"]![name]!["values"]!["sensitivity"]!.GetValue<double>()!=before["displays"]![name]!["values"]!["sensitivity"]!.GetValue<double>())throw new Exception("Editing one display changed another");displays.SelectedItem=name;profile=name;BuildPage();}
   checks.Add("Native slider updates renderer; "+names.Length+" display profiles remain independent");
   if(state["audio"]?.GetValue<string>().StartsWith("WASAPI",StringComparison.Ordinal)==true){
   state=await Request(Command("audio"));await Task.Delay(200);state=await Request(Command("get"));if(state["audioRunning"]!.GetValue<bool>())throw new Exception("Capture did not stop");
   state=await Request(Command("audio"));await Task.Delay(300);state=await Request(Command("get"));if(!state["audioRunning"]!.GetValue<bool>()||state["sampleRate"]!.GetValue<int>()==0||state["fft"]!.GetValue<int>()==0)throw new Exception("Capture did not resume with valid diagnostics");
   checks.Add("WASAPI capture stops and resumes; silence retains sample rate and FFT diagnostics");
   }else checks.Add("WASAPI hardware test skipped: no default output endpoint on this runner");
  }catch(Exception ex){failure=ex.ToString();}
  finally{
   try{pending.Clear();if(changed){profile=first;await Request(Command("set","sensitivity",JsonValue.Create(original)));}await Request(Command("property","IndependentDisplays",before["independent"]!.DeepClone(),true));if(state["audioRunning"]?.GetValue<bool>()!=before["audioRunning"]?.GetValue<bool>())await Request(Command("audio"));}catch(Exception ex){failure=(failure??"")+" Restore: "+ex;}
  }
  Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output))!);await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{valid=failure==null,displays=names,checks,error=failure},new JsonSerializerOptions{WriteIndented=true}));
  await Request(Command("test-finish"));Close();
 }
}
