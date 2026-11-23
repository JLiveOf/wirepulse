using System.Diagnostics;
namespace WirePulse;

// One PCM stream: introduction once, then the sustain section forever. Keeping
// the output device open avoids a gap or a repeated intro between welcome pages.
public sealed class MusicSequence
{
 readonly byte[] intro,loop;int position;bool looping;
 public int SampleRate{get;}public int Channels{get;}public volatile bool Muted;
 public MusicSequence(string introPath,string loopPath){var a=Load(introPath);var b=Load(loopPath);if(a.rate!=b.rate||a.channels!=b.channels)throw new InvalidDataException("Welcome music formats differ");intro=a.pcm;loop=b.pcm;SampleRate=a.rate;Channels=a.channels;}
 public MusicSequence(byte[] intro,byte[] loop,int sampleRate=48000,int channels=2){if(intro.Length==0||loop.Length==0)throw new ArgumentException("Empty music section");this.intro=intro;this.loop=loop;SampleRate=sampleRate;Channels=channels;}
 public static (byte[] pcm,int rate,int channels) Load(string path){using var r=new BinaryReader(File.OpenRead(path));if(new string(r.ReadChars(4))!="RIFF")throw new InvalidDataException("Expected WAV music");r.ReadInt32();if(new string(r.ReadChars(4))!="WAVE")throw new InvalidDataException("Expected PCM music");int rate=0,channels=0,bits=0,code=0;byte[]? data=null;while(r.BaseStream.Position+8<=r.BaseStream.Length){string id=new(r.ReadChars(4));int size=r.ReadInt32();if(size<0||size>32*1024*1024||r.BaseStream.Position+size>r.BaseStream.Length)throw new InvalidDataException("Invalid music section");long end=r.BaseStream.Position+size+(size&1);if(id=="fmt "){code=r.ReadInt16();channels=r.ReadInt16();rate=r.ReadInt32();r.ReadBytes(6);bits=r.ReadInt16();}else if(id=="data")data=r.ReadBytes(size);r.BaseStream.Position=end;}if(code!=1||bits!=16||channels is <1 or >2||rate<8000||data is not{Length:>0}||data.Length%(channels*2)!=0)throw new InvalidDataException("Expected 16-bit PCM music");return(data,rate,channels);}
 public int Read(byte[] buffer,int offset,int count){ArgumentOutOfRangeException.ThrowIfNegative(offset);ArgumentOutOfRangeException.ThrowIfNegative(count);if(count>buffer.Length-offset)throw new ArgumentException("Invalid music buffer");int written=0;while(written<count){var section=looping?loop:intro;int n=Math.Min(count-written,section.Length-position);Buffer.BlockCopy(section,position,buffer,offset+written,n);position+=n;written+=n;if(position==section.Length){position=0;looping=true;}}if(Muted)Array.Clear(buffer,offset,count);return count;}
}

sealed class SetupMusic:IDisposable
{
 MusicSequence? sequence;bool started,muted;int disposed;readonly CancellationTokenSource stop=new();
 public bool Muted{get=>muted;set{muted=value;if(sequence!=null)sequence.Muted=value;}}
#if WINDOWS
 NAudio.Wave.WaveOutEvent? output;
 sealed class Provider(MusicSequence sequence):NAudio.Wave.IWaveProvider{public NAudio.Wave.WaveFormat WaveFormat{get;}=new(sequence.SampleRate,16,sequence.Channels);public int Read(byte[] buffer,int offset,int count)=>sequence.Read(buffer,offset,count);}
#else
 Process? player;
#endif
 public void Start(){if(started||Volatile.Read(ref disposed)!=0)return;started=true;try{var folder=Path.Combine(AppContext.BaseDirectory,"Assets");sequence=new(Path.Combine(folder,"Setup-intro.wav"),Path.Combine(folder,"Setup-loop.wav")){Muted=muted};
#if WINDOWS
  output=new(){DesiredLatency=100};output.Init(new Provider(sequence));output.Play();
#else
  player=new(){StartInfo=new("pacat"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardError=true}};
  foreach(var arg in new[]{"--playback","--raw","--format=s16le","--rate="+sequence.SampleRate,"--channels="+sequence.Channels,"--latency-msec=80","--client-name=WirePulse"})player.StartInfo.ArgumentList.Add(arg);
  player.Start();_=player.StandardError.ReadToEndAsync();var stream=player.StandardInput.BaseStream;var pcm=sequence;var token=stop.Token;
  _=Task.Run(async()=>{try{var buffer=new byte[8192];while(!token.IsCancellationRequested){pcm.Read(buffer,0,buffer.Length);await stream.WriteAsync(buffer,token);}}catch(Exception e)when(e is IOException or OperationCanceledException or ObjectDisposedException){Trace.WriteLine(e.Message);}});
#endif
 }catch(Exception e){Trace.WriteLine("Setup music: "+e);Dispose();}}
 public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Cancel();
#if WINDOWS
  try{output?.Stop();}catch(Exception e){Trace.WriteLine(e);}output?.Dispose();output=null;
#else
  try{if(player is{HasExited:false})player.Kill();}catch(InvalidOperationException){}player?.Dispose();player=null;
#endif
  sequence=null;
 }
}
