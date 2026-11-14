using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
#if WINDOWS
using NAudio.CoreAudioApi;
using NAudio.Wave;
#endif
namespace WirePulse;

public sealed class AudioCapture(Spectrum spectrum) : IDisposable
{
    readonly CancellationTokenSource stop = new(); public string Status { get; private set; } = "Conectando audio del sistema"; Task? worker;
    public void Start() => worker = Task.Run(Run);
#if WINDOWS
 WasapiLoopbackCapture? capture;string endpoint="";float[] converted=[];
 async Task Run(){using var enumerator=new MMDeviceEnumerator();while(!stop.IsCancellationRequested){try{using var device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);if(endpoint!=device.ID||capture==null){capture?.Dispose();capture=new WasapiLoopbackCapture(device);endpoint=device.ID;capture.DataAvailable+=(_,e)=>{var f=capture?.WaveFormat;if(f==null)return;int bytes=f.BitsPerSample/8,count=e.BytesRecorded/bytes;if(converted.Length<count)converted=new float[count];bool floating=f.Encoding==WaveFormatEncoding.IeeeFloat||(f is WaveFormatExtensible ext&&ext.SubFormat==new Guid("00000003-0000-0010-8000-00aa00389b71"));for(int i=0;i<count;i++){int p=i*bytes;converted[i]=floating?BitConverter.ToSingle(e.Buffer,p):bytes switch{2=>BitConverter.ToInt16(e.Buffer,p)/32768f,3=>((e.Buffer[p]|e.Buffer[p+1]<<8|e.Buffer[p+2]<<16)<<8>>8)/8388608f,4=>BitConverter.ToInt32(e.Buffer,p)/2147483648f,_=>0};}spectrum.Push(converted,count,f.Channels,f.SampleRate);};capture.RecordingStopped+=(_,e)=>{if(e.Exception!=null){Status="Audio desconectado: "+e.Exception.Message;endpoint="";}};capture.StartRecording();Status=$"WASAPI loopback · {capture.WaveFormat.SampleRate} Hz";}}catch(Exception e){Status=e.Message;endpoint="";}try{await Task.Delay(2000,stop.Token);}catch{break;}}capture?.Dispose();capture=null;}
#else
    Process? process; string sink = ""; CancellationTokenSource? captureStop;
    async Task Run()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var info = await Commands.Run("pactl", "--format=json", "info"); if (info.Code != 0) throw new Exception("Instala pulseaudio-utils y comprueba PulseAudio/PipeWire."); var name = JsonNode.Parse(info.Text)?["default_sink_name"]?.GetValue<string>(); if (string.IsNullOrWhiteSpace(name)) throw new Exception("No se encuentra la salida de audio predeterminada."); if (sink != name || process == null || process.HasExited)
                {
                    captureStop?.Cancel(); try { process?.Kill(true); } catch { }
                    ; sink = name; int rate = 48000; var list = await Commands.Run("pactl", "--format=json", "list", "sinks"); if (list.Code == 0 && JsonNode.Parse(list.Text) is JsonArray entries) { foreach (var entry in entries) { if (entry?["name"]?.GetValue<string>() != name) continue; var spec = entry["sample_specification"]?.GetValue<string>() ?? ""; var match = System.Text.RegularExpressions.Regex.Match(spec, @"(\d+)Hz"); if (match.Success) rate = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture); } }
                    captureStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); process = new Process { StartInfo = new() { FileName = "parec", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } }; foreach (var a in new[] { "--device=" + name + ".monitor", "--format=float32le", "--rate=" + rate, "--channels=2", "--raw", "--client-name=WirePulse" }) process.StartInfo.ArgumentList.Add(a); process.Start(); _ = process.StandardError.ReadToEndAsync(); _ = Read(process, rate, captureStop.Token); Status = $"Monitor {name} · {rate} Hz";
                }
            }
            catch (Exception e) { Status = e.Message; sink = ""; }
            try { await Task.Delay(2000, stop.Token); } catch { break; }
        }
    }
    async Task Read(Process p, int rate, CancellationToken token) { try { var bytes = new byte[8192]; var floats = new float[2048]; int offset = 0; while (!token.IsCancellationRequested) { int n = await p.StandardOutput.BaseStream.ReadAsync(bytes.AsMemory(offset, bytes.Length - offset), token); if (n == 0) break; int total = offset + n, aligned = total - total % 8; Buffer.BlockCopy(bytes, 0, floats, 0, aligned); spectrum.Push(floats, aligned / 4, 2, rate); offset = total - aligned; if (offset > 0) Buffer.BlockCopy(bytes, aligned, bytes, 0, offset); } } catch (OperationCanceledException) { } catch (Exception e) { Status = e.Message; } }
#endif
    public void Dispose()
    {
        stop.Cancel();
#if !WINDOWS
        captureStop?.Cancel(); try { process?.Kill(true); } catch { }
#endif
        try { worker?.Wait(TimeSpan.FromSeconds(3)); } catch { }
        stop.Dispose();
    }
}
