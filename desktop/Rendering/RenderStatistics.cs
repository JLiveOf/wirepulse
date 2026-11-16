using System.Diagnostics;
namespace WirePulse;

public sealed class RenderStatistics
{
    long frames; double sampleStart = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, lastFps, averageMs, lastFrame; long sampleFrames; readonly object gate = new();
    public long Frames => Interlocked.Read(ref frames);
    public double Fps { get { lock (gate) return Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency-lastFrame>1.5?0:lastFps; } }
    public double DrawMs { get { lock (gate) return averageMs; } }
    public string Backend { get; internal set; } = "Esperando primer fotograma";
    public void Record(double milliseconds) { Interlocked.Increment(ref frames); lock (gate) { averageMs = averageMs == 0 ? milliseconds : averageMs * .95 + milliseconds * .05; double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; lastFrame=now; if (now - sampleStart >= 1) { lastFps = (Frames - sampleFrames) / (now - sampleStart); sampleFrames = Frames; sampleStart = now; } } }
}
