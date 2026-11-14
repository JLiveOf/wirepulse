using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using WirePulse;

AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var id="ui-test-"+Guid.NewGuid();var root=new Settings(id);var left=new Settings(id+"left",root);var right=new Settings(id+"right",root);
var displays=new Dictionary<string,Settings>{{"Left",left},{"Right",right}};
var window=new SettingsWindow(root,new Spectrum(),new MediaService(root),()=>"Fixture",displays);
window.Show();
var groups=window.GetVisualDescendants().OfType<ComboBox>().First(x=>x.ItemsSource!.Cast<object>().Contains("Orbe"));
for(int group=0;group<6;group++){groups.SelectedIndex=group;Avalonia.Threading.Dispatcher.UIThread.RunJobs();var specs=root.Specs.Where(x=>x.Group==groups.SelectedItem?.ToString()&&x.Key!="quality").ToArray();if(window.GetVisualDescendants().OfType<Slider>().Count()<specs.Count(x=>x.Kind=="slider")||window.GetVisualDescendants().OfType<CheckBox>().Count()<specs.Count(x=>x.Kind=="bool")+1)throw new Exception("Missing settings controls in "+groups.SelectedItem);Console.WriteLine("PASS settings group "+groups.SelectedItem);}
groups.SelectedItem="Orbe";Avalonia.Threading.Dispatcher.UIThread.RunJobs();
var sphere=window.GetVisualDescendants().OfType<CheckBox>().First(x=>x.Content?.ToString()=="Mostrar orbe");sphere.IsChecked=false;if(root.On("sphere"))throw new Exception("Orb toggle did not update configuration");
var mode=window.GetVisualDescendants().OfType<CheckBox>().First(x=>x.Content?.ToString()?.Contains("independiente")==true);mode.IsChecked=true;Avalonia.Threading.Dispatcher.UIThread.RunJobs();
var selector=window.GetVisualDescendants().OfType<ComboBox>().First(x=>x.ItemsSource!.Cast<object>().Contains("Right"));selector.SelectedItem="Right";Avalonia.Threading.Dispatcher.UIThread.RunJobs();
sphere=window.GetVisualDescendants().OfType<CheckBox>().First(x=>x.Content?.ToString()=="Mostrar orbe");sphere.IsChecked=false;if(right.On("sphere")||!left.On("sphere"))throw new Exception("Editing one display changed another");
Console.WriteLine("PASS native settings checkboxes and independent display selection");window.Close();
foreach(var key in new[]{id,id+"left",id+"right"}){var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..16];File.Delete(Path.Combine(Settings.Folder,"display-"+hash+".json"));}
