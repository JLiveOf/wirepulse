namespace WirePulse;

/// Carries fractional deadlines forward: 60 on a 64/75/165 Hz clock must not become 32/37/55.
public sealed class FramePacer
{
    double next = double.NaN, rate;
    public bool Due(double seconds, double fps)
    {
        fps = Math.Clamp(fps, 1, 1000); if (fps != rate || double.IsNaN(next) || seconds < next - 1) { rate = fps; next = seconds; }
        if (seconds + 1e-6 < next) return false; double step = 1 / fps; next += step; if (next < seconds - step) next = seconds + step; return true;
    }
}
