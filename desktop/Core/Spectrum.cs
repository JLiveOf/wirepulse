using System.Diagnostics;
namespace WirePulse;

public readonly record struct SpectrumFrame(double[] Bands, double[] Low, double[] High, int SampleRate, int FftSize, double PeakHz, long Frames, double Age);

/// One complex FFT packs two real channels; reconstruction preserves stereo power, including antiphase.
public sealed class Spectrum
{
    readonly object sync = new(); int n, rate, cursor, hop; double[] left = [], right = [], window = [], re = [], im = [], power = [], cos = [], sin = [], low = [], high = []; int[] reverse = [], starts = [], ends = []; double sum, last; long frames;
    SpectrumFrame published = new(new double[64], new double[64], new double[64], 0, 0, 0, 0, 999);
    public void Configure(int sampleRate)
    {
        if (rate == sampleRate) return; if (sampleRate < 8000 || sampleRate > 768000) throw new ArgumentOutOfRangeException(nameof(sampleRate)); rate = sampleRate; n = 8192; while (n < rate / 6d && n < 65536) n *= 2;
        left = new double[n]; right = new double[n]; window = new double[n]; re = new double[n]; im = new double[n]; power = new double[n / 2 + 1]; reverse = new int[n]; cos = new double[n / 2]; sin = new double[n / 2]; sum = 0; int bits = System.Numerics.BitOperations.Log2((uint)n);
        for (int i = 0; i < n; i++) { window[i] = .5 - .5 * Math.Cos(2 * Math.PI * i / n); sum += window[i]; int x = i, r = 0; for (int j = 0; j < bits; j++) { r = (r << 1) | (x & 1); x >>= 1; } reverse[i] = r; }
        for (int i = 0; i < n / 2; i++) { double angle = -2 * Math.PI * i / n; cos[i] = Math.Cos(angle); sin[i] = Math.Sin(angle); }
        cursor = hop = 0; starts = new int[64]; ends = new int[64]; ends[0] = 1; int boundary = 1;
        for (int b = 0; b < 63; b++) { int end = b == 62 ? n / 2 + 1 : Math.Max(boundary + 1, Math.Min(n / 2 + 1 - (62 - b), (int)Math.Round(Math.Pow(n / 2 + 1, (b + 1) / 63d)))); starts[b + 1] = boundary; ends[b + 1] = end; boundary = end; }
        low = new double[64]; high = new double[64]; for (int b = 0; b < 64; b++) { low[b] = b == 0 ? 0 : (starts[b] - .5) * rate / n; high[b] = Math.Min(rate / 2d, (ends[b] - .5) * rate / n); }
    }
    public void Push(float[] data, int samples, int channels, int sampleRate)
    {
        if (channels < 1 || samples < 0 || samples > data.Length) throw new ArgumentOutOfRangeException(nameof(samples)); Configure(sampleRate); int mask = n - 1;
        for (int f = 0; f < samples / channels; f++) { float l = data[f * channels], r = channels > 1 ? data[f * channels + 1] : l; left[cursor] = float.IsFinite(l) ? l : 0; right[cursor] = float.IsFinite(r) ? r : 0; cursor = (cursor + 1) & mask; if (++hop >= Math.Max(1, rate / 60)) { hop = 0; Analyze(); } }
    }
    void Analyze()
    {
        int mask = n - 1; for (int i = 0; i < n; i++) { int r = reverse[i], p = (cursor + i) & mask; re[r] = left[p] * window[i]; im[r] = right[p] * window[i]; }
        for (int len = 2; len <= n; len <<= 1) { int half = len / 2, stride = n / len; for (int i = 0; i < n; i += len) for (int j = 0; j < half; j++) { int a = i + j, b = a + half, k = j * stride; double wr = cos[k], wi = sin[k], tr = wr * re[b] - wi * im[b], ti = wr * im[b] + wi * re[b]; re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti; } }
        int peak = 0; for (int k = 0; k <= n / 2; k++) { int mirror = (n - k) & mask; double lr = (re[k] + re[mirror]) * .5, li = (im[k] - im[mirror]) * .5, rr = (im[k] + im[mirror]) * .5, ri = (re[mirror] - re[k]) * .5, scale = (k == 0 || k == n / 2 ? 1 : 2) / sum; power[k] = (lr * lr + li * li + rr * rr + ri * ri) * scale * scale * .5; if (power[k] > power[peak]) peak = k; }
        var bands = new double[64]; for (int b = 0; b < 64; b++) { double v = 0; for (int k = starts[b]; k < ends[b]; k++) v += power[k]; v = Math.Sqrt(v / (ends[b] - starts[b])); bands[b] = Math.Clamp((20 * Math.Log10(Math.Max(1e-7, v)) + 72) / 60, 0, 1); }
        lock (sync) { last = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; published = new(bands, low, high, rate, n, peak * (double)rate / n, ++frames, 0); }
    }
    public SpectrumFrame Snapshot() { lock (sync) return published with { Age = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - last }; }
}
