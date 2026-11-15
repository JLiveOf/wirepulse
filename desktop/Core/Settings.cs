using System.Text.Json;
using System.Text.Json.Nodes;
namespace WirePulse;

public sealed record ControlSpec(string Key, string Title, string Kind, JsonNode? Value, string Group, double Min, double Max, double Step);
public sealed class Settings
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WirePulse");
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy=JsonNamingPolicy.CamelCase, WriteIndented = true };
    public List<ControlSpec> Specs { get; } = JsonSerializer.Deserialize<List<ControlSpec>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "controls.json")), Json)!;
    public Dictionary<string, JsonNode?> Values { get; set; } = [];
    public string MediaSource { get; set; } = "Automático";
    public bool TopCentered { get; set; } = true;
    public double TopMargin { get; set; } = 4.8;
    public string CiderUrl { get; set; } = "http://127.0.0.1:10767";
    public string CiderToken { get; set; } = "";
    public SceneDocument? Scene{get;set;}
    public bool EditorSnap{get;set;}=true;
    public bool Ephemeral{get;set;}
    public string WindowsEngine{get;set;}=RenderEngines.Skia;
    public string WindowsRenderer {get;set;}="Automático";
    public string RenderProfile { get; set; } = "Automático";
    public bool SuspendFullscreen { get; set; } = true;
    public string BackgroundStyle{get;set;}="Estrellas";
    public string ClockFont{get;set;}="System";
    public bool StartupAsked { get; set; }
    public bool Paused { get; set; }
    public bool IndependentDisplays { get; set; }
    readonly string file;
    public bool HasSaved => File.Exists(Path.Combine(Folder, file));
    public event Action? Changed;
    public Settings(string? profile = null, Settings? template = null) { file = profile == null ? "settings.json" : "display-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(profile)))[..16] + ".json"; Reset(false); if (template != null) { Values = template.Values.ToDictionary(x => x.Key, x => x.Value?.DeepClone()); TopCentered = template.TopCentered; TopMargin = template.TopMargin; Scene=template.Scene?.Clone();EditorSnap=template.EditorSnap;RenderProfile = template.RenderProfile; SuspendFullscreen = template.SuspendFullscreen;BackgroundStyle=template.BackgroundStyle;ClockFont=template.ClockFont; } try { var n = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, file)))!;Scene=n["scene"]?.Deserialize<SceneDocument>(Json);Scene?.Sanitize();EditorSnap=n["editorSnap"]?.GetValue<bool>()??true;WindowsEngine=RenderEngines.Normalize(n["windowsEngine"]?.GetValue<string>());WindowsRenderer=GraphicsBackend.Normalize(n["windowsRenderer"]?.GetValue<string>()); BackgroundStyle=n["backgroundStyle"]?.GetValue<string>()??BackgroundStyle;ClockFont=n["clockFont"]?.GetValue<string>()??ClockFont;StartupAsked = n["startupAsked"]?.GetValue<bool>() ?? false; SuspendFullscreen = n["suspendFullscreen"]?.GetValue<bool>() ?? true; RenderProfile = n["renderProfile"]?.GetValue<string>() ?? "Automático"; IndependentDisplays = n["independentDisplays"]?.GetValue<bool>() ?? false; foreach (var kv in n["values"]!.AsObject()) if (Values.ContainsKey(kv.Key)) Values[kv.Key] = kv.Value?.DeepClone(); MediaSource = n["mediaSource"]?.GetValue<string>() ?? "Automático"; TopCentered = n["topCentered"]?.GetValue<bool>() ?? true; TopMargin = n["topMargin"]?.GetValue<double>() ?? 4.8; CiderUrl = n["ciderUrl"]?.GetValue<string>() ?? CiderUrl; CiderToken = n["ciderToken"]?.GetValue<string>() ?? ""; } catch { } Sanitize(); }
    void Sanitize() { foreach (var spec in Specs) { var value = Values[spec.Key] as JsonValue; bool valid = false; if (spec.Kind == "slider" && value != null && value.TryGetValue<double>(out var number) && double.IsFinite(number)) { Values[spec.Key] = JsonValue.Create(Math.Clamp(number, spec.Min, spec.Max)); valid = true; } else if (spec.Kind is "bool" or "toggle") valid = value != null && (value.TryGetValue<bool>(out _) || value.TryGetValue<double>(out _)); else if (value != null && value.TryGetValue<string>(out var text)) valid = spec.Kind != "color" || Avalonia.Media.Color.TryParse(text, out _); if (!valid) Values[spec.Key] = spec.Value?.DeepClone(); } if(BackgroundStyle is not("Estrellas" or "Malla"))BackgroundStyle="Estrellas";TopMargin = double.IsFinite(TopMargin) ? Math.Clamp(TopMargin, 0, 80) : 4.8; if (RenderProfile is not ("Automático" or "Calidad" or "Ligero")) RenderProfile = "Automático"; }
    public double N(string key) => Values[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : 0;
    public bool On(string key) => Values[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : N(key) > .5;
    public string Text(string key) => Values[key]?.GetValue<string>() ?? "";
    public void Set(string key, object value) { Values[key] = JsonSerializer.SerializeToNode(value); Save(); }
    public void Reset(bool save = true) { Values = Specs.ToDictionary(x => x.Key, x => x.Value?.DeepClone()); Scene=null;TopCentered = true; TopMargin = 4.8;BackgroundStyle="Estrellas";ClockFont="System"; if (save) Save(); }
    public Settings Preview(){var copy=new Settings("preview-"+Guid.NewGuid(),this){Ephemeral=true,ClockFont=ClockFont,BackgroundStyle=BackgroundStyle,Scene=Scene?.Clone()};return copy;}
    public void Save() { if(Ephemeral){Changed?.Invoke();return;}Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, file), JsonSerializer.Serialize(new { windowsEngine=WindowsEngine,scene=Scene,editorSnap=EditorSnap,windowsRenderer=WindowsRenderer,backgroundStyle=BackgroundStyle,clockFont=ClockFont,startupAsked = StartupAsked, suspendFullscreen = SuspendFullscreen, renderProfile = RenderProfile, independentDisplays = IndependentDisplays, values = Values, mediaSource = MediaSource, topCentered = TopCentered, topMargin = TopMargin, ciderUrl = CiderUrl, ciderToken = CiderToken }, Json)); if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(Folder, file), UnixFileMode.UserRead | UnixFileMode.UserWrite); Changed?.Invoke(); }
}
