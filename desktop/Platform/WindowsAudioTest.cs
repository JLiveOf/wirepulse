#if WINDOWS
using System.Diagnostics;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace WirePulse;
internal static class WindowsAudioTest {
 public static int Run(){
  object report;bool valid;
  using var enumerator=new MMDeviceEnumerator();MMDevice device;
  try{device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);}catch{Write(new{valid=true,skipped="No default WASAPI output endpoint"});return 0;}
  using(device){
   var spectrum=new Spectrum();using var capture=new AudioCapture(spectrum);
   using var output=new WasapiOut(device,AudioClientShareMode.Shared,true,50);
   var tone=new SignalGenerator(48000,2){Frequency=1000,Gain=.01,Type=SignalGeneratorType.Sin};
   capture.Start();output.Init(tone);output.Play();var watch=Stopwatch.StartNew();SpectrumFrame frame;
   do{Thread.Sleep(50);frame=spectrum.Snapshot();}while(watch.Elapsed.TotalSeconds<3&&(frame.Frames<10||Math.Abs(frame.PeakHz-1000)>20));
   output.Stop();capture.Dispose();capture.Dispose();
   valid=frame.Frames>=10&&Math.Abs(frame.PeakHz-1000)<20&&frame.Bands.Any(b=>b>0);
   report=new{valid,rate=frame.SampleRate,fft=frame.FftSize,peakHz=frame.PeakHz,frames=frame.Frames,elapsedSeconds=watch.Elapsed.TotalSeconds,status=capture.Status};
  }
  Write(report);return valid?0:1;
 }
 static void Write(object report){string output=Program.Option("--probe-output")??"windows-audio.json";Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));}
}
#endif
