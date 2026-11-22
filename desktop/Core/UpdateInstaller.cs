using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
namespace WirePulse;
public static class UpdateInstaller
{
 static string Sh(string value)=>"'"+value.Replace("'","'\"'\"'")+"'";
 static string Ps(string value)=>"'"+value.Replace("'","''")+"'";
 public static string WindowsScript(string package,string directory,string tag,int pid,string log)=>"$ErrorActionPreference='Stop'\nWait-Process -Id "+pid+" -ErrorAction SilentlyContinue\n$ok=$false\ntry { $p=Start-Process -FilePath "+Ps(package)+" -ArgumentList "+Ps("/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /DIR=\""+directory+"\"")+" -PassThru -Wait; if($p.ExitCode -ne 0){throw ('Installer exit '+$p.ExitCode)}; $ok=$true } catch { $_ | Out-File "+Ps(log)+" }\nif($ok){ & "+Ps(Path.Combine(directory,"WirePulse.exe"))+" --record-release "+Ps(tag)+" }\nStart-Process -FilePath "+Ps(Path.Combine(directory,"WirePulse.exe"))+" -WorkingDirectory "+Ps(directory)+"\n";
 public static string PortableScript(string directory,string content,string backup,int pid,string tag)=>"#!/bin/sh\nwhile kill -0 "+pid+" 2>/dev/null; do sleep 1; done\nif mv "+Sh(directory)+" "+Sh(backup)+"; then\n if mv "+Sh(content)+" "+Sh(directory)+"; then "+Sh(Path.Combine(directory,"WirePulse"))+" --record-release "+Sh(tag)+"\n "+Sh(Path.Combine(directory,"WirePulse"))+" >/dev/null 2>&1 &\n else mv "+Sh(backup)+" "+Sh(directory)+"; "+Sh(Path.Combine(directory,"WirePulse"))+" >/dev/null 2>&1 &\n fi\nelse "+Sh(Path.Combine(directory,"WirePulse"))+" >/dev/null 2>&1 &\nfi\n";
 public static async Task<bool> StartAsync(string package,string tag){
  string updates=Path.GetDirectoryName(package)!,directory=Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
  if(OperatingSystem.IsWindows()){
   string script=Path.Combine(updates,"install-"+Guid.NewGuid().ToString("N")+".ps1"),exe=Path.Combine(directory,"WirePulse.exe");
   string arguments="/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /DIR=\""+directory+"\"";
   await File.WriteAllTextAsync(script,WindowsScript(package,directory,tag,Environment.ProcessId,Path.Combine(updates,"install-error.txt")),new UTF8Encoding(false));
   var helper=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=updates};foreach(string arg in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(await File.ReadAllTextAsync(script)))})helper.ArgumentList.Add(arg);_ = Process.Start(helper)??throw new IOException("No se pudo iniciar el instalador.");return true;
  }
  if(package.EndsWith(".deb",StringComparison.OrdinalIgnoreCase)){await UpdateService.RecordDebPackageAsync(package,tag);var open=new ProcessStartInfo("xdg-open"){UseShellExecute=false};open.ArgumentList.Add(package);_ = Process.Start(open)??throw new IOException("No se pudo abrir el instalador de paquetes.");return false;}
  // Portable Linux: prepare beside the installation so replacement is atomic and permissions are retained.
  string parent=Path.GetDirectoryName(directory)!,stage=Path.Combine(parent,".wirepulse-update-"+Guid.NewGuid().ToString("N")),backup=directory+".previous-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(stage);
  try{using var source=File.OpenRead(package);using var gzip=new GZipStream(source,CompressionMode.Decompress);await TarFile.ExtractToDirectoryAsync(gzip,stage,false);string content=File.Exists(Path.Combine(stage,"WirePulse"))?stage:Directory.EnumerateDirectories(stage).SingleOrDefault(d=>File.Exists(Path.Combine(d,"WirePulse")))??stage;string executable=Path.Combine(content,"WirePulse");if(!File.Exists(executable))throw new InvalidDataException("El paquete no contiene WirePulse.");var probeInfo=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true};probeInfo.ArgumentList.Add("--version");using(var probe=Process.Start(probeInfo)??throw new IOException("No se pudo comprobar el paquete.")){using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));string version=await probe.StandardOutput.ReadToEndAsync(deadline.Token);await probe.WaitForExitAsync(deadline.Token);if(probe.ExitCode!=0||ReleaseVersion.Parse(version.Trim())?.Core!=ReleaseVersion.Parse(tag)?.Core)throw new InvalidDataException("Versión de actualización no válida.");}string script=Path.Combine(updates,"install-"+Guid.NewGuid().ToString("N")+".sh");await File.WriteAllTextAsync(script,PortableScript(directory,content,backup,Environment.ProcessId,tag));var helper=new ProcessStartInfo("/bin/sh"){UseShellExecute=false,WorkingDirectory=updates};helper.ArgumentList.Add(script);_ = Process.Start(helper)??throw new IOException("No se pudo iniciar el instalador.");return true;}catch{Directory.Delete(stage,true);throw;}
 }
}
