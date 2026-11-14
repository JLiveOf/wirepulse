using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using WirePulse;

AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var id="ui-test-"+Guid.NewGuid();var root=new Settings(id);var left=new Settings(id+"left",root);var right=new Settings(id+"right",root);
var displays=new Dictionary<string,Settings>{{"Left",left},{"Right",right}};
var startupFolder=Path.Combine(Path.GetTempPath(),"wirepulse-ui-startup-"+Guid.NewGuid());
var window=new SettingsWindow(root,new Spectrum(),new MediaService(root),()=>"Fixture",displays,startupFactory:()=>new StartupRegistration("/tmp/WirePulse",startupFolder,windows:false));
window.Show();
var groups=window.GetVisualDescendants().OfType<ComboBox>().First(x=>x.ItemsSource!.Cast<object>().Contains("Orbe"));
for(int group=0;group<7;group++){groups.SelectedIndex=group;Avalonia.Threading.Dispatcher.UIThread.RunJobs();var specs=root.Specs.Where(x=>x.Group==groups.SelectedItem?.ToString()&&x.Key!="quality").ToArray();if(window.GetVisualDescendants().OfType<Slider>().Count()<specs.Count(x=>x.Kind=="slider")||window.GetVisualDescendants().OfType<CheckBox>().Count()<specs.Count(x=>x.Kind=="bool")+1)throw new Exception("Missing settings controls in "+groups.SelectedItem);Console.WriteLine("PASS settings group "+groups.SelectedItem);}
groups.SelectedItem="Orbe";Avalonia.Threading.Dispatcher.UIThread.RunJobs();
var sphere=window.GetVisualDescendants().OfType<CheckBox>().First(x=>x.Content?.ToString()=="Mostrar orbe");sphere.IsChecked=false;if(root.On("sphere"))throw new Exception("Orb toggle did not update configuration");
var mode=window.GetVisualDescendants().OfType<CheckBox>().First(x=>x.Content?.ToString()?.Contains("independiente")==true);mode.IsChecked=true;Avalonia.Threading.Dispatcher.UIThread.RunJobs();
var selector=window.GetVisualDescendants().OfType<ComboBox>().First(x=>x.ItemsSource!.Cast<object>().Contains("Right"));selector.SelectedItem="Right";Avalonia.Threading.Dispatcher.UIThread.RunJobs();
sphere=window.GetVisualDescendants().OfType<CheckBox>().First(x=>x.Content?.ToString()=="Mostrar orbe");sphere.IsChecked=false;if(right.On("sphere")||!left.On("sphere"))throw new Exception("Editing one display changed another");
Console.WriteLine("PASS native settings checkboxes and independent display selection");groups.SelectedItem="Inicio";Avalonia.Threading.Dispatcher.UIThread.RunJobs();var start=window.GetVisualDescendants().OfType<CheckBox>().First(x=>x.Content?.ToString()=="Abrir WirePulse al iniciar sesión");start.IsChecked=true;if(!root.StartupAsked||!File.Exists(Path.Combine(startupFolder,"wirepulse.desktop")))throw new Exception("Startup settings did not enable consented registration");start.IsChecked=false;if(File.Exists(Path.Combine(startupFolder,"wirepulse.desktop")))throw new Exception("Startup settings did not remove registration");Directory.Delete(startupFolder,true);Console.WriteLine("PASS startup settings enable/disable without changing this computer's login items");
int decision=-1;var consent=new StartupPrompt(value=>{decision=value?1:0;return null;});consent.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();var no=consent.GetVisualDescendants().OfType<Button>().First(b=>b.Content?.ToString()=="Ahora no");no.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));if(decision!=0||consent.IsVisible)throw new Exception("Decline did not close startup prompt without enabling it");Console.WriteLine("PASS startup dialog honours decline");
window.Close();
foreach(var key in new[]{id,id+"left",id+"right"}){var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..16];File.Delete(Path.Combine(Settings.Folder,"display-"+hash+".json"));}
