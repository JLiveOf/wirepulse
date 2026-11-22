using System.Text.Json;
namespace WirePulse;
internal static class LocalizationTest
{
 public static int Run(){
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS localization: "+name);}
  Check(L10n.Resolve("system","es-MX")=="es"&&L10n.Resolve("system","en-GB")=="en"&&L10n.Resolve("system","fr-FR")=="en"&&L10n.Resolve("es","en-US")=="es"&&L10n.Resolve("en","es-ES")=="en","system locale, fallback and explicit choice");
  Check(L10n.Translate("Guardar diseño","en")=="Save layout"&&L10n.Translate("Guardar diseño","es")=="Guardar diseño","English and Spanish labels");
  Check(L10n.Translate("64 bandas · FFT 8192 · análisis 0–24000 Hz","en")=="64 bands · FFT 8192 · analysis 0–24000 Hz","dynamic diagnostics preserve numbers");
  Check(L10n.Translate("Error42 λ-日本語","en")=="Error42 λ-日本語","unknown and Unicode strings preserved");
  Check(L10n.Translate("Leyendo portada de Cider…","en")=="Reading artwork from Cider…","dynamic status longest phrase");
  Check(L10n.Translate("No se pudo importar: fixture.png","en")=="Could not import: fixture.png","error prefix preserves file names");
  using var controls=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","controls.json")));
  foreach(var spec in controls.RootElement.EnumerateArray()){string title=spec.GetProperty("title").GetString()!;Check(L10n.Translate(title,"en")!=title||title=="Color","control "+spec.GetProperty("key").GetString());}
  return 0;
 }
}
