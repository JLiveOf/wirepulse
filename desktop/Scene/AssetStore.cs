using System.IO.Compression;
namespace WirePulse;

public static class AssetStore {
 public static string Root=>Path.Combine(Settings.Folder,"Assets");
 public static async Task<string> ImportMediaAsync(string path){
  if(!BackdropMedia.IsVideo(path))return await Task.Run(()=>Import(path));
  if(!File.Exists(path)||new FileInfo(path).Length>2L*1024*1024*1024)throw new InvalidDataException("El vídeo debe ocupar como máximo 2 GB.");
  string folder=Path.Combine(Root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string target=Path.Combine(folder,Path.GetFileName(path));
  try{await using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,true);await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,true);await input.CopyToAsync(output);return target;}catch{Directory.Delete(folder,true);throw;}
 }
 public static string Import(string path,bool widget=false){Directory.CreateDirectory(Root);string folder=Path.Combine(Root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{
  if(Directory.Exists(path)){long total=0;int count=0;foreach(var dir in Directory.EnumerateDirectories(path,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint})){if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("No se admiten enlaces.");}foreach(string file in Directory.EnumerateFiles(path,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint})){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("El widget contiene enlaces no compatibles.");var info=new FileInfo(file);total+=info.Length;if(total>128*1024*1024||++count>2048)throw new InvalidDataException("El paquete es demasiado grande.");string output=Path.Combine(folder,Path.GetRelativePath(path,file));Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(file,output);}return Entry(folder);}
  if(Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase)){using var zip=ZipFile.OpenRead(path);long total=0;if(zip.Entries.Count>2048)throw new InvalidDataException("Demasiados archivos en el widget.");foreach(var entry in zip.Entries){string name=entry.FullName.Replace('\\','/');string output=Path.GetFullPath(Path.Combine(folder,name));if(!output.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.Ordinal)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Ruta no válida en el paquete.");total+=entry.Length;if(total>128*1024*1024)throw new InvalidDataException("El paquete es demasiado grande.");if(name.EndsWith('/'))Directory.CreateDirectory(output);else{Directory.CreateDirectory(Path.GetDirectoryName(output)!);entry.ExtractToFile(output);}}return Entry(folder);}
  if(new FileInfo(path).Length>128*1024*1024)throw new InvalidDataException("El archivo es demasiado grande.");string target=Path.Combine(folder,Path.GetFileName(path));File.Copy(path,target);return target;
 }catch{Directory.Delete(folder,true);throw;}}
 static string Entry(string folder){string? entry=Directory.EnumerateFiles(folder,"index.html",SearchOption.AllDirectories).OrderBy(p=>p.Length).FirstOrDefault()??Directory.EnumerateFiles(folder,"*.html",SearchOption.AllDirectories).FirstOrDefault();return entry??throw new InvalidDataException("El widget necesita un archivo index.html.");}
}
