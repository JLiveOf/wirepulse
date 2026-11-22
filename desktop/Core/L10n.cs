using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace WirePulse;

/// Display strings only. Serialized settings, enum values, paths and user content stay unchanged.
public static class L10n
{
    public static readonly string PreferencePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"WirePulse","language.txt");
    public static string Preference { get { try{return Normalize(File.ReadAllText(PreferencePath).Trim());}catch{return "system";} } }
    static string Normalize(string value)=>value is "es" or "en"?value:"system";
    public static void SetPreference(string value){Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);File.WriteAllText(PreferencePath,Normalize(value));}
    public static readonly string Language=Resolve(Environment.GetEnvironmentVariable("WIREPULSE_LANGUAGE")??Preference,CultureInfo.CurrentUICulture.Name);
    public static string Resolve(string requested,string system)=>Normalize(requested) is var mode && mode!="system"?mode:system.StartsWith("es",StringComparison.OrdinalIgnoreCase)?"es":"en";
    static readonly Dictionary<string,string> English=Load();
    static readonly Regex Pattern=new(string.Join("|",English.Keys.OrderByDescending(k=>k.Length).Select(k=>(char.IsLetter(k[0])?@"(?<![\p{L}])":"")+Regex.Escape(k)+(char.IsLetter(k[^1])?@"(?![\p{L}])":""))),RegexOptions.Compiled|RegexOptions.CultureInvariant);
    static Dictionary<string,string> Load(){var assembly=typeof(L10n).Assembly;using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith("Assets.en.json",StringComparison.Ordinal)));return JsonSerializer.Deserialize<Dictionary<string,string>>(stream!)!;}
    public static string Text(string? value)=>Translate(value??"",Language);
    public static string Translate(string value,string language){if(language!="en"||value.Length==0)return value;if(English.TryGetValue(value,out var exact))return exact;return Pattern.Replace(value,m=>English[m.Value]);}
}
