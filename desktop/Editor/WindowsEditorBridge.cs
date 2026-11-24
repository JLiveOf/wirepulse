#if WINDOWS
using System.Diagnostics;
namespace WirePulse;

internal sealed partial class WindowsSettingsHost {
 Process? editorProcess;
 public bool ShowEditor(){
  string exe=Path.Combine(AppContext.BaseDirectory,"WindowsSettings","WirePulse.Settings.exe");if(!File.Exists(exe))return false;
  if(editorProcess is{HasExited:false}){editorProcess.Refresh();ShowWindow(editorProcess.MainWindowHandle,9);SetForegroundWindow(editorProcess.MainWindowHandle);return true;}
  serverTask??=Serve();var start=new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe)!};start.ArgumentList.Add("--pipe");start.ArgumentList.Add(pipe);start.ArgumentList.Add("--editor");
  if(Program.Option("--winui-editor-test-output") is string output){start.ArgumentList.Add("--editor-smoke-output");start.ArgumentList.Add(Path.GetFullPath(output));if(Program.Args.Contains("--editor-test-discard"))start.ArgumentList.Add("--editor-discard-test");}
  editorProcess=Process.Start(start);return editorProcess!=null;
 }
}
#endif
