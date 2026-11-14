using Avalonia;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
namespace WirePulse;
internal sealed class LayeredFrameOperation(WindowsLayeredPresenter presenter,Rect bounds):ICustomDrawOperation {
 public Rect Bounds=>bounds;public bool HitTest(Point point)=>false;public bool Equals(ICustomDrawOperation? other)=>false;public void Dispose(){}
 public void Render(ImmediateDrawingContext context){var api=context.TryGetFeature<ISkiaSharpApiLeaseFeature>();if(api==null)return;using var lease=api.Lease();if(lease.SkSurface is{} surface)presenter.Capture(surface);}
}
