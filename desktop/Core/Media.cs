using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
namespace WirePulse;

public sealed record Track(string Provider = "Audio del sistema", string Title = "", string Artist = "", string Album = "", double Duration = 0, double Position = 0, bool Playing = false, string Artwork = "", byte[]? ImageBytes = null)
{
    public DateTimeOffset Updated { get; init; } = DateTimeOffset.UtcNow;
    public double Elapsed => Math.Clamp(Position + (Playing ? (DateTimeOffset.UtcNow - Updated).TotalSeconds : 0), 0, Math.Max(0, Duration));
}
public static class Commands
{
    public static async Task<(int Code, string Text)> Run(string file, params string[] args) { using var p = new Process { StartInfo = new() { FileName = file, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } }; foreach (var a in args) p.StartInfo.ArgumentList.Add(a); try { p.Start(); var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync(); using var c = new CancellationTokenSource(TimeSpan.FromSeconds(8)); try { await p.WaitForExitAsync(c.Token); } catch { p.Kill(true); return (-1, ""); } await stderr; return (p.ExitCode, (await stdout).Trim()); } catch { return (-1, ""); } }
}
public sealed class MediaService(Settings settings)
{
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(3) };
    public Track Current { get; private set; } = new(); public string Status { get; private set; } = "Esperando reproductor";
    public async Task Poll()
    {
        try
        {
            Track? cider = null; if (settings.MediaSource is "Automático" or "Cider") try { cider = await Cider(); } catch { }

            if (settings.MediaSource == "Cider" && cider != null) { Current = cider; Status = "Leyendo Cider"; return; }
            var native = await Native(); Current = cider?.Playing == true ? cider : native?.Playing == true ? native : cider ?? native ?? new(); Status = Current.Title.Length > 0 ? "Leyendo " + Current.Provider : "Sin canción activa";
        }
        catch (Exception e) { Status = e.Message; }
    }
    async Task<Track?> Native()
    {
#if WINDOWS
  return await WindowsMedia.Read(settings.MediaSource);
#else
        var names = await Commands.Run("playerctl", "--list-all"); Track? paused = null;
        foreach (var id in names.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(24))
        {
            async Task<string> Query(params string[] a) { var result = await Commands.Run("playerctl", new[] { "--player", id }.Concat(a).ToArray()); return result.Code == 0 ? result.Text : ""; }
            var state = await Query("status"); if (state.Length == 0) continue;
            var title = await Query("metadata", "xesam:title"); if (title.Length == 0) continue;
            var url = await Query("metadata", "xesam:url"); var provider = Provider(id, url);
            if (settings.MediaSource != "Automático" && !provider.Contains(settings.MediaSource, StringComparison.OrdinalIgnoreCase)) continue;
            double.TryParse(await Query("metadata", "mpris:length"), NumberStyles.Float, CultureInfo.InvariantCulture, out var length); double.TryParse(await Query("position"), NumberStyles.Float, CultureInfo.InvariantCulture, out var pos);
            var track = new Track(provider, title, await Query("metadata", "xesam:artist"), await Query("metadata", "xesam:album"), length / 1e6, pos, state == "Playing", await Query("metadata", "mpris:artUrl")); if (track.Playing) return track; paused ??= track;
        }
        return paused;
#endif
    }
    public static string Provider(string id, string url = "") { var x = id.ToLowerInvariant(); if (x.Contains("cider")) return "Cider"; if (x.Contains("spotify")) return "Spotify"; if (Uri.TryCreate(url, UriKind.Absolute, out var u)) { if (u.Host == "music.youtube.com") return "YouTube Music"; if (u.Host == "youtube.com" || u.Host.EndsWith(".youtube.com") || u.Host == "youtu.be") return "YouTube"; if (u.Host == "music.apple.com") return "Apple Music"; } if (x.Contains("applemusic") || x.Contains("apple.music")) return "Apple Music"; if (x.Contains("chrome") || x.Contains("chromium")) return "Chrome"; if (x.Contains("firefox")) return "Firefox"; return id.Split('.')[0]; }
    async Task<Track?> Cider()
    {
        if (!Uri.TryCreate(settings.CiderUrl, UriKind.Absolute, out var host) || !host.IsLoopback || host.Scheme != "http") return null;
        var authToken=settings.CiderToken;async Task<JsonNode?> Get(string path) { using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(host, path)); if (authToken.Length > 0) req.Headers.Add("apptoken", authToken); using var r = await Http.SendAsync(req); if (r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { Status = "Cider requiere un token de Playback válido"; return null; } return r.IsSuccessStatusCode ? JsonNode.Parse(await r.Content.ReadAsStringAsync()) : null; }
        var v2 = (await Get("/api/v2/playback"))?["data"]; var info = v2?["nowPlaying"]; bool playing; double position, duration;
        if (info != null) { playing = v2?["state"]?.GetValue<string>() == "playing"; position = v2?["time"]?["currentTime"]?.GetValue<double>() ?? 0; duration = v2?["time"]?["duration"]?.GetValue<double>() ?? info["durationInMillis"]?.GetValue<double>() / 1000 ?? 0; }
        else { info = (await Get("/api/v1/playback/now-playing"))?["info"]; if (info == null) return null; playing = (await Get("/api/v1/playback/is-playing"))?["is_playing"]?.GetValue<bool>() == true; position = info["currentPlaybackTime"]?.GetValue<double>() ?? 0; duration = info["durationInMillis"]?.GetValue<double>() / 1000 ?? 0; }
        return new("Cider", info["name"]?.GetValue<string>() ?? "", info["artistName"]?.GetValue<string>() ?? "", info["albumName"]?.GetValue<string>() ?? "", duration, position, playing, (info["artwork"]?["url"]?.GetValue<string>() ?? "").Replace("{w}", "600").Replace("{h}", "600"));
    }
}
