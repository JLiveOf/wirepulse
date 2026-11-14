using System.Runtime.InteropServices;
using SkiaSharp;
namespace WirePulse;

/// Counted form of Skia's public C API. SKCanvas.DrawPoints exposes only whole arrays;
/// using its exact C ABI lets reusable buffers submit just their visible portion.
internal static unsafe class SkiaLines
{
    public static bool Available => draw != null;
    static readonly delegate* unmanaged[Cdecl]<IntPtr, SKPointMode, nuint, SKPoint*, IntPtr, void> draw = Load();
    static delegate* unmanaged[Cdecl]<IntPtr, SKPointMode, nuint, SKPoint*, IntPtr, void> Load() { try { var module = NativeLibrary.Load("libSkiaSharp", typeof(SKCanvas).Assembly, null); return (delegate* unmanaged[Cdecl]<IntPtr, SKPointMode, nuint, SKPoint*, IntPtr, void>)NativeLibrary.GetExport(module, "sk_canvas_draw_points"); } catch { return null; } }
    public static void Draw(SKCanvas canvas, SKPoint[] points, int count, SKPaint paint, SKPath fallback,SKPointMode mode=SKPointMode.Lines) { if (count == 0) return; if (draw != null) { fixed (SKPoint* p = points) draw(canvas.Handle, mode, (nuint)count, p, paint.Handle); return; } if(mode==SKPointMode.Points){for(int i=0;i<count;i++)canvas.DrawPoint(points[i],paint);return;}fallback.Rewind(); for (int i = 0; i < count; i += 2) { fallback.MoveTo(points[i]); fallback.LineTo(points[i + 1]); } canvas.DrawPath(fallback, paint); }
}
