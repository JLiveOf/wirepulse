using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System.Diagnostics;
using System.Numerics;
namespace WirePulse;

public sealed record VisualizerOptions(bool Sphere, bool Full, bool Spectrum, bool Segmented, int Density, double Radius, double X, double Y, double Deformation, double Pulse, double Rotation, double Line, double Glow, double Opacity, SKColor Color, double SpectrumX, double SpectrumY, double SpectrumW, double SpectrumH, double Gate, double Sensitivity, double Bass, double Smoothing, string Profile)
{
    public SpaceOptions? Space{get;init;}
    static SKColor ToSkia(string text) { var c = Avalonia.Media.Color.Parse(text); return new SKColor(c.R, c.G, c.B, c.A); }
    public static VisualizerOptions Read(Settings s) => new(s.On("sphere"), s.On("fullmesh"), s.On("spectrum"), s.On("segmented"), (int)s.N("density"), s.N("radius"), s.N("sphereX"), s.N("sphereY"), s.N("deformation"), s.N("pulse"), s.N("rotation"), s.N("linewidth"), s.N("glow"), s.N("opacity"), ToSkia(s.Text("wirecolor")), s.N("spectrumX"), s.N("spectrumY"), s.N("spectrumWidth"), s.N("spectrumHeight"), s.N("gate"), s.N("sensitivity"), s.N("bass"), s.N("smoothing"), s.RenderProfile){Space=SpaceOptions.Read(s)};
}
public sealed class VisualizerPainter : IDisposable
{
    readonly object gate = new();readonly SpaceBackground space=new(); readonly double[] smooth = new double[64], peaks = new double[64]; double last, angle, detail = 1; int density; OrbMesh? mesh; Vector3[] projected = []; SKPoint[] far = [], near = []; SKPath? segments; double segmentWidth, segmentHeight, segmentStep;
    readonly SKPaint line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Butt }; readonly SKPaint fill = new() { IsAntialias = false }; readonly SKPaint gradient = new() { IsAntialias = false }; readonly SKPath rings = new(), fallback = new(), cpuFar = new(), cpuNear = new(); bool disposed;
    public RenderStatistics Statistics { get; } = new();
    public VisualizerPainter() { gradient.Shader = SKShader.CreateLinearGradient(new(0, 0), new(1, 0), new[] { new SKColor(87, 255, 0), new SKColor(184, 219, 0), new SKColor(255, 99, 0), new SKColor(255, 0, 40) }, new[] { 0f, .4f, .75f, 1f }, SKShaderTileMode.Clamp); }
    public void Draw(SKCanvas canvas, double width, double height, VisualizerOptions o, SpectrumFrame audio, bool paused, bool gpu)
    {
        lock (gate)
        {
            if (disposed) return; long start = Stopwatch.GetTimestamp(); double now = start / (double)Stopwatch.Frequency, dt = last == 0 ? 1 / 60d : Math.Clamp(now - last, 0, .1); last = now; double decay = Math.Pow(o.Smoothing, dt * 60), bass = 0; int bassCount = 0;
            for (int i = 0; i < 64; i++) { bool isBass = audio.High[i] > 20 && audio.Low[i] < 160; double input = audio.Age < .3 && !paused ? audio.Bands[i] : 0, v = Math.Clamp(Math.Max(0, input - o.Gate) * o.Sensitivity * (isBass ? o.Bass : 1), 0, 1); smooth[i] = v + (smooth[i] - v) * decay; if (smooth[i] < .0001) smooth[i] = 0; if (isBass) { bass += smooth[i]; bassCount++; } }
            double beat=audio.Age<.8&&!paused?audio.Beat*Math.Exp(-audio.Age*7):0;if(o.Space is{} background)space.Draw(canvas,width,height,now,dt,background,beat,audio.Bpm,paused,gpu,o.Profile=="Ligero");
            if (!paused) angle += dt * o.Rotation; if (o.Sphere) Orb(canvas, width, height, now, bassCount > 0 ? bass / bassCount : 0, o, gpu); if (o.Spectrum) Leds(canvas, width, height, dt, o);
            Statistics.Backend = GraphicsBackend.Description(gpu); Statistics.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }
    void Orb(SKCanvas c, double w, double h, double time, double bass, VisualizerOptions o, bool gpu)
    {
        int wanted = Math.Clamp(o.Density, 8, 72); if (o.Profile == "Ligero") wanted = Math.Min(wanted, 24); else if (o.Profile == "Automático") wanted = Math.Clamp((int)(wanted * Volatile.Read(ref detail)), Math.Min(12, wanted), wanted);
        if (mesh == null || density != wanted) { density = wanted; mesh = new(density); projected = new Vector3[mesh.Vertices.Length]; far = new SKPoint[mesh.Edges.Length * 2]; near = new SKPoint[far.Length]; }
        double radius = h * o.Radius / 100 * (1 + bass * o.Pulse), x = w * o.X / 100, y = h * o.Y / 100; mesh.Project(projected, smooth, angle, time, radius, x, y, o.Deformation, o.Full);
        int nf = 0, nn = 0; float pad = (float)(o.Glow + o.Line); foreach (var (a, b) in mesh.Edges) { var p = projected[a]; var q = projected[b]; double z = (p.Z + q.Z) * .5; if (!o.Full && Math.Abs(z) > .25) continue; if (Math.Max(p.X, q.X) < -pad || Math.Min(p.X, q.X) > w + pad || Math.Max(p.Y, q.Y) < -pad || Math.Min(p.Y, q.Y) > h + pad) continue; var target = z > 0 ? near : far; int n = z > 0 ? nn : nf; target[n++] = new(p.X, p.Y); target[n++] = new(q.X, q.Y); if (z > 0) nn = n; else nf = n; }
        // Fixed reusable buffers avoid native paths, tessellation and per-frame managed vertex arrays.

        double glow = o.Profile == "Ligero" ? 0 : o.Glow * (o.Profile == "Automático" ? Math.Pow(Volatile.Read(ref detail), 2) : 1); for (int pass = 0; pass < 2; pass++) { var points = pass == 0 ? far : near; if ((pass == 0 ? nf : nn) == 0) continue; int count = pass == 0 ? nf : nn; var cpu = pass == 0 ? cpuFar : cpuNear; if (!gpu) { cpu.Rewind(); for (int i = 0; i < count; i += 2) { cpu.MoveTo(points[i]); cpu.LineTo(points[i + 1]); } } void Draw() { if (gpu) SkiaLines.Draw(c, points, count, line, fallback); else c.DrawPath(cpu, line); } double opacity = o.Opacity * (pass == 0 ? .24 : .85); if (glow > 0) { line.Color = o.Color.WithAlpha((byte)(255 * opacity * .08)); line.StrokeWidth = (float)(o.Line + glow); Draw(); } line.Color = o.Color.WithAlpha((byte)(255 * opacity)); line.StrokeWidth = (float)o.Line; Draw(); }
        if (!o.Full) { rings.Reset(); for (int j = 0; j < 14; j++) { for (int i = 0; i <= 48; i++) { double a = i / 48d * Math.PI * 2, r = radius * (.48 + j * .007 + smooth[i % 48] * o.Deformation * .42), t = a + Math.Sin(a * 3 + time * .3 + j * .13) * .09; float px = (float)(x + Math.Cos(t) * r), py = (float)(y + Math.Sin(t) * r * .94); if (i == 0) rings.MoveTo(px, py); else rings.LineTo(px, py); } rings.Close(); } line.Color = o.Color.WithAlpha((byte)(255 * o.Opacity * .48)); line.StrokeWidth = (float)o.Line; c.DrawPath(rings, line); }
    }
    void Leds(SKCanvas c, double w, double h, double dt, VisualizerOptions o)
    {
        double x = w * o.SpectrumX / 100, y = h * o.SpectrumY / 100, height = h * o.SpectrumH / 100, max = w * o.SpectrumW / 100, row = height / 64, step = Math.Max(4, w / 240), bar = Math.Max(1, row * .68);
        c.Save(); c.Translate((float)x, (float)y); if (o.Segmented) { if (segments == null || segmentWidth != max || segmentHeight != height || segmentStep != step) { segments?.Dispose(); segments = new(); for (double k = 0; k < max; k += step) segments.AddRect(new((float)k, 0, (float)Math.Min(k + step - 1, max), (float)height)); segmentWidth = max; segmentHeight = height; segmentStep = step; } c.ClipPath(segments, SKClipOperation.Intersect, false); }
        for (int i = 0; i < 64; i++) { double len = smooth[63 - i] * max; peaks[i] = Math.Max(len, peaks[i] - dt * max * .25); if (len < .01) continue; c.Save(); c.Translate(0, (float)(i * row)); c.Scale((float)len, (float)bar); c.DrawRect(0, 0, 1, 1, gradient); c.Restore(); }
        c.Restore(); fill.Color = new SKColor(255, 30, 60, 115); for (int i = 0; i < 64; i++) if (peaks[i] > 3) c.DrawRect((float)(x + peaks[i]), (float)(y + i * row), 1, (float)bar, fill);
    }
    public void Adapt(double targetFps) { double budget = 1000 / Math.Max(1, targetFps), current = Volatile.Read(ref detail); if (Statistics.DrawMs > budget * .65 || (Statistics.Frames > 120 && Statistics.Fps > 0 && Statistics.Fps < targetFps * .85)) current = Math.Max(.5, current - .05); else if (Statistics.DrawMs < budget * .25) current = Math.Min(1, current + .025); Volatile.Write(ref detail, current); }
    public void Dispose() { lock (gate) { if (disposed) return; disposed = true;space.Dispose(); line.Dispose(); fill.Dispose(); gradient.Shader?.Dispose(); gradient.Dispose(); segments?.Dispose(); rings.Dispose(); fallback.Dispose(); cpuFar.Dispose(); cpuNear.Dispose(); } }
}
public sealed class VisualizationOperation(VisualizerPainter painter, VisualizerOptions options, SpectrumFrame audio, bool paused, Rect bounds) : ICustomDrawOperation
{
    public Rect Bounds => bounds; public bool HitTest(Point p) => false; public bool Equals(ICustomDrawOperation? other) => false; public void Dispose() { }
    public void Render(ImmediateDrawingContext context) { var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>(); if (feature == null) return; using var lease = feature.Lease();if(OperatingSystem.IsWindows()&&!GraphicsBackend.Observed){using var platform=lease.TryLeasePlatformGraphicsApi();GraphicsBackend.Observe(lease.GrContext,platform?.Context.GetType().FullName);} painter.Draw(lease.SkCanvas, bounds.Width, bounds.Height, options, audio, paused, lease.GrContext != null); }
}
