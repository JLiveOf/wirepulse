using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
namespace WirePulse;

/// A second launch opens the existing settings instead of running another audio/render loop.
internal sealed class SingleInstance : IDisposable
{
    readonly Mutex mutex; readonly string pipeName; readonly CancellationTokenSource stop = new(); readonly bool owned; Task? listener;
    public bool Owned => owned;
    public SingleInstance(string suffix = "") { string user = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName)))[..12]; pipeName = "WirePulse-" + user + suffix; mutex = new Mutex(false, (OperatingSystem.IsWindows() ? "Local\\" : "") + pipeName); try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; } }
    public void Notify() { try { using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly); pipe.Connect(500); pipe.WriteByte(1); } catch { } }
    public void Listen(Action show) { if (!owned || listener != null) return; listener = Task.Run(async () => { while (!stop.IsCancellationRequested) { try { using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly); await pipe.WaitForConnectionAsync(stop.Token); var message = new byte[1]; if (await pipe.ReadAsync(message, stop.Token) > 0) show(); } catch (OperationCanceledException) { break; } catch { if (!stop.IsCancellationRequested) await Task.Delay(100); } } }); }
    public void Dispose() { stop.Cancel(); try { listener?.Wait(1000); } catch { } if (owned) mutex.ReleaseMutex(); mutex.Dispose(); stop.Dispose(); }
}
