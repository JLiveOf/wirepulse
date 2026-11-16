using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;

namespace WirePulse.WindowsSettings;
internal static class Program {
 [STAThread] static void Main(string[] args){
  AppDomain.CurrentDomain.UnhandledException+=(_,e)=>File.WriteAllText(Path.Combine(Path.GetTempPath(),"WirePulse-settings-error.txt"),e.ExceptionObject.ToString());
  WinRT.ComWrappersSupport.InitializeComWrappers();
  Application.Start(_=>{SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));new SettingsApplication(args);});
 }
}
sealed partial class SettingsApplication:Application {
 Window? window;NativeEditorShell? editor;readonly string[] args;
 public SettingsApplication(string[] args){this.args=args;UnhandledException+=(_,e)=>{File.WriteAllText(Path.Combine(Path.GetTempPath(),"WirePulse-settings-error.txt"),e.Exception.ToString());};InitializeComponent();RequestedTheme=ApplicationTheme.Dark;}
 protected override void OnLaunched(LaunchActivatedEventArgs args){try{if(this.args.Contains("--editor")){editor=new NativeEditorShell(this.args);editor.Start();}else{window=new SettingsWindow(this.args);window.Activate();}}catch(Exception ex){File.WriteAllText(Path.Combine(Path.GetTempPath(),"WirePulse-settings-error.txt"),ex.ToString());throw;}}
}
