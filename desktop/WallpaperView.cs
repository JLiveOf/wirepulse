using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;
namespace WirePulse;

/// Independent retained layers: static background and music text are not rebuilt at audio frame rate.
public sealed class WallpaperView : Panel, IDisposable
{
    readonly Settings settings; readonly Spectrum spectrum; readonly MediaService media; readonly VisualizerPainter painter = new(); readonly Layer backdrop, animation, card,clock;
    VisualizerOptions options; Bitmap? cover, background; string artKey = "", backgroundPath = ""; bool loading, disposed; byte[]? artBytes; DateTime retryAt; readonly CancellationTokenSource artworkStop = new(); string measuredProvider = ""; double measuredTagSize, measuredTagWidth; double clockAt;string lastClock="",lastFont="",lastClockColor="";double lastClockSize;FormattedText? clockText;double cardAt; Track? previousTrack;
    readonly Dictionary<string, IBrush> brushes = []; readonly Dictionary<(string, double, bool, IBrush), FormattedText> textCache = [];
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    public Settings Configuration => settings; public long Frames => painter.Statistics.Frames; public RenderStatistics Statistics => painter.Statistics;
    public WallpaperView(Settings settings, Spectrum spectrum, MediaService media)
    {
        this.settings = settings; this.spectrum = spectrum; this.media = media; options = VisualizerOptions.Read(settings); ClipToBounds = true;
        backdrop = new(c => DrawBackground(c, Bounds.Width, Bounds.Height)); animation = new(c => c.Custom(new VisualizationOperation(painter, options, spectrum.Snapshot(), settings.Paused, new Rect(Bounds.Size)))); card = new(c => { if (settings.On("card") && (!settings.On("hideidle") || media.Current.Title.Length > 0)) Card(c, Bounds.Width, Bounds.Height); });
        clock=new Layer(DrawClock);Children.Add(backdrop); Children.Add(animation); Children.Add(card);Children.Add(clock); settings.Changed += ConfigurationChanged;
    }
    IBrush Brush(string color) { if (!brushes.TryGetValue(color, out var brush)) { brush = new SolidColorBrush(Color.Parse(color)); brushes[color] = brush; } return brush; }
    public void Advance(double seconds) { if (disposed) return; animation.InvalidateVisual();if(seconds-clockAt>=1){clockAt=seconds;clock.InvalidateVisual();} if (seconds - cardAt >= .25 || !ReferenceEquals(previousTrack, media.Current)) { cardAt = seconds; previousTrack = media.Current; card.InvalidateVisual(); } }
    public void Adapt(double target) => painter.Adapt(target);
    void ConfigurationChanged() { if (disposed) return; options = VisualizerOptions.Read(settings); textCache.Clear();clock.InvalidateVisual(); backdrop.InvalidateVisual(); animation.InvalidateVisual(); card.InvalidateVisual(); }
    void DrawBackground(DrawingContext c, double w, double h)
    {
        c.FillRectangle(Brush(settings.Text("backgroundcolor")), new Rect(0, 0, w, h)); var path = settings.Text("backgroundimage"); if (path != backgroundPath) { backgroundPath = path; background?.Dispose(); background = null; try { if (File.Exists(path)) background = new Bitmap(path); } catch { } }
        if (background != null) { DrawCover(c, background, new Rect(0, 0, w, h)); c.FillRectangle(new SolidColorBrush(Color.FromArgb((byte)(255 * settings.N("dim") / 100), 0, 0, 0)), new Rect(0, 0, w, h)); }
    }
    void Text(DrawingContext c, string text, double x, double y, double width, double size, IBrush? color = null, bool bold = false) { var ink = color ?? Brush("#ffffff"); var key = (text, size, bold, ink); if (!textCache.TryGetValue(key, out var formatted)) { if (textCache.Count > 40) textCache.Clear(); formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, ink); textCache[key] = formatted; } using (c.PushClip(new Rect(x, y, Math.Max(0, width), size * 1.6))) c.DrawText(formatted, new Point(x, y)); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { base.OnDetachedFromVisualTree(e); Dispose(); }
    public void Dispose() { if (disposed) return; disposed = true; settings.Changed -= ConfigurationChanged; painter.Dispose(); artworkStop.Cancel(); cover?.Dispose(); background?.Dispose(); }
    sealed class Layer(Action<DrawingContext> draw) : Control { public override void Render(DrawingContext context) { base.Render(context); if (Bounds.Width > 0 && Bounds.Height > 0) draw(context); } }
    void DrawClock(DrawingContext c){if(!settings.On("clockshow"))return;string value=ClockText.Format(DateTime.Now,settings.On("clock24")),font=settings.ClockFont,color=settings.Text("clockcolor");double size=settings.N("clocksize");if(clockText==null||value!=lastClock||font!=lastFont||size!=lastClockSize||color!=lastClockColor){lastClock=value;lastFont=font;lastClockSize=size;lastClockColor=color;clockText=new FormattedText(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,font=="System"?Typeface.Default:new Typeface(font),size,Brush(color));}double x=Math.Clamp(Bounds.Width*settings.N("clockX")/100-clockText.Width/2,0,Math.Max(0,Bounds.Width-clockText.Width)),y=Math.Clamp(Bounds.Height*settings.N("clockY")/100-clockText.Height/2,0,Math.Max(0,Bounds.Height-clockText.Height));c.DrawText(clockText,new Point(x,y));}
    double ProviderWidth(string provider, double size) { if (measuredProvider != provider || measuredTagSize != size) { measuredProvider = provider; measuredTagSize = size; measuredTagWidth = new FormattedText(provider, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), size, Brush("#ffffff")).Width; } return measuredTagWidth; }
    void Card(DrawingContext c, double w, double h)
    {
        var t = media.Current; var scale = settings.N("cardscale"); double width = Math.Min(850, Math.Max(260, w * .44)) * scale, height = Math.Max(130, h * .21) * scale, x = settings.TopCentered ? (w - width) / 2 : w * settings.N("cardX") / 100, y = settings.TopCentered ? h * settings.TopMargin / 100 : h - h * settings.N("cardY") / 100 - height, pad = Math.Max(12, w * .015 * scale), art = Math.Max(46, h * .10 * scale); c.DrawRectangle(Brush("#ee1d2024"), new Pen(Brush("#55ffffff"), 1), new Rect(x, y, width, height), h * .033 * scale, h * .033 * scale);
        UpdateArtwork(t); var imageRect = new Rect(x + pad, y + pad, art, art); if (cover != null) { using (c.PushClip(new RoundedRect(imageRect, art * .2))) DrawCover(c, cover, imageRect); } else { c.DrawRectangle(Brush("#554a3d50"), null, imageRect, art * .2, art * .2); Text(c, "♫", imageRect.X + art * .3, imageRect.Y + art * .1, art, art * .5); }
        double textX = x + pad + art + pad, gap = pad, tagWidth = Math.Min(width * .34, Math.Max(50, ProviderWidth(t.Provider, Math.Max(9, h * .016 * scale)) + 18)); Text(c, t.Title.Length > 0 ? t.Title : "Audio del sistema", textX, y + pad, width - (textX - x) - tagWidth - pad * 2, Math.Max(13, h * .027 * scale), bold: true); Text(c, t.Artist.Length > 0 ? t.Artist : "WirePulse", textX, y + pad + h * .033 * scale, width - (textX - x) - tagWidth - pad * 2, Math.Max(11, h * .0225 * scale), Brush("#ccffffff")); Text(c, t.Album, textX, y + pad + h * .063 * scale, width - (textX - x) - pad, Math.Max(10, h * .019 * scale), Brush("#99ffffff")); c.DrawRectangle(Brush("#22ffffff"), null, new Rect(x + width - pad - tagWidth, y + pad + art * .3, tagWidth, Math.Max(20, h * .028 * scale)), 6, 6); Text(c, t.Provider, x + width - pad - tagWidth + 6, y + pad + art * .3 + 3, tagWidth - 12, Math.Max(9, h * .016 * scale));
        double lineY = y + height - pad - Math.Max(13, h * .019 * scale) - 12, lineW = width - pad * 2; var progress = t.Duration > 0 ? t.Elapsed / t.Duration : 0; c.DrawRectangle(Brush("#44ffffff"), null, new Rect(x + pad, lineY, lineW, 4 * scale), 2, 2); if (progress > 0) c.DrawRectangle(Brush(settings.Text("accent")), null, new Rect(x + pad, lineY, lineW * progress, 4 * scale), 2, 2); string Stamp(double a) => $"{(int)Math.Max(0, a) / 60}:{(int)Math.Max(0, a) % 60:00}"; Text(c, Stamp(t.Elapsed), x + pad, lineY + 8, lineW / 2, Math.Max(10, h * .019 * scale), Brush("#99ffffff")); Text(c, t.Duration > 0 ? "−" + Stamp(t.Duration - t.Elapsed) : "—", x + width - pad - 60, lineY + 8, 60, Math.Max(10, h * .019 * scale), Brush("#99ffffff"));
    }
    static void DrawCover(DrawingContext c, Bitmap image, Rect rect) { var sz = image.Size; double f = Math.Max(rect.Width / sz.Width, rect.Height / sz.Height), sw = rect.Width / f, sh = rect.Height / f; c.DrawImage(image, new Rect((sz.Width - sw) / 2, (sz.Height - sh) / 2, sw, sh), rect); }
    void UpdateArtwork(Track track)
    {
        string key = string.Join('|', track.Provider, track.Title, track.Artist, track.Album, track.Artwork);
        if (key == artKey && ReferenceEquals(artBytes, track.ImageBytes) && (cover != null || loading || DateTime.UtcNow < retryAt)) return;
        artKey = key; artBytes = track.ImageBytes; cover?.Dispose(); cover = null; retryAt = DateTime.UtcNow.AddSeconds(5);
        if (track.ImageBytes is { Length: > 0 }) { try { using var stream = new MemoryStream(track.ImageBytes); cover = Bitmap.DecodeToWidth(stream, 512); } catch { } return; }
        if (loading || track.Artwork.Length == 0) return; loading = true;
        _ = Task.Run(async () => { Bitmap? image = null; try { if (Uri.TryCreate(track.Artwork, UriKind.Absolute, out var u)) { byte[] bytes; if (u.IsFile) bytes = await File.ReadAllBytesAsync(u.LocalPath, artworkStop.Token); else if (u.Scheme == "https") bytes = await Http.GetByteArrayAsync(u, artworkStop.Token); else return; if (bytes.Length <= 20 * 1024 * 1024) { using var stream = new MemoryStream(bytes); image = Bitmap.DecodeToWidth(stream, 512); } } } catch { } finally { await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { loading = false; if (!disposed && artKey == key) { cover = image; card.InvalidateVisual(); } else { image?.Dispose(); if (!disposed) artKey = ""; } }); } });
    }
}
