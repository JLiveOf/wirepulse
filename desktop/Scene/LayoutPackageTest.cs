using System.IO.Compression;
using System.Text.Json;
using Avalonia;
namespace WirePulse;
public static class LayoutPackageTest {
 public static int Run(string fixture,string output){string temp=Path.Combine(Path.GetTempPath(),"wirepulse-layout-test-"+Guid.NewGuid());Directory.CreateDirectory(temp);try{var package=LayoutPackage.Import(fixture,temp);var item=package.Scene.Items.First(i=>i.Kind=="image");if(Math.Abs(item.X-.125)>.00001||Math.Abs(item.Rotation-32)>.0001||!File.Exists(item.Asset)||!File.Exists(package.Background.Asset))throw new Exception("Portable layout geometry/resources changed");string widget=package.Scene.Items.First(i=>i.Kind=="widget").Asset;if(!File.Exists(Path.Combine(Path.GetDirectoryName(widget)!,"script.js")))throw new Exception("Widget resources missing");var settings=new Settings("layout-test-"+Guid.NewGuid()){Ephemeral=true};package.Apply(settings);LayoutPackage.Export(output,settings,new Size(1920,1080));var round=LayoutPackage.Import(output,temp);if(round.Scene.Items.Count!=package.Scene.Items.Count||!File.ReadAllBytes(round.Scene.Items.First(i=>i.Kind=="image").Asset).AsSpan().SequenceEqual(File.ReadAllBytes(item.Asset)))throw new Exception("Layout roundtrip lost resources");
 string bad=Path.Combine(temp,"traversal.zip");using(var archive=ZipFile.Open(bad,ZipArchiveMode.Create)){using var w=new StreamWriter(archive.CreateEntry("../escaped.txt").Open());w.Write("invalid");}try{LayoutPackage.Import(bad,temp);throw new Exception("Unsafe layout was accepted");}catch(InvalidDataException){}
 using(var zip=ZipFile.OpenRead(output)){using var reader=new StreamReader(zip.GetEntry("layout.json")!.Open());string manifest=reader.ReadToEnd();if(manifest.Contains("ciderToken")||manifest.Contains("windowsRenderer")||manifest.Contains(Path.GetFullPath(temp)))throw new Exception("Layout leaked machine preferences/absolute paths");}
 Console.WriteLine("PASS portable layout geometry, styles, image/video/widget resources, roundtrip and traversal rejection");return 0;
 }finally{Directory.Delete(temp,true);}}
}
