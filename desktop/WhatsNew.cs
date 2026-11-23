using System.Text.Json;
namespace WirePulse;
public sealed record WhatsNewFeature(string Icon,string Title,string Description);
public sealed record WhatsNewContent(string Version,WhatsNewFeature[] Features)
{
 public static WhatsNewContent Current{get;}=Load();
 static WhatsNewContent Load(){try{var notes=JsonSerializer.Deserialize<WhatsNewContent>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","whats-new.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});if(notes?.Features is{Length:>0 and <=8}&&notes.Features.All(x=>!string.IsNullOrWhiteSpace(x.Title)&&!string.IsNullOrWhiteSpace(x.Description)))return notes;}catch{}return new(UpdateService.BuildVersion,[new("sparkles","Novedades de WirePulse","Consulta las notas de esta versión en GitHub.")]);}
}
