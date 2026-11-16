using Avalonia;
using System.Diagnostics;
namespace WirePulse;

internal static class Program
{
    public static string[] Args = [];
    public static SingleInstance? Singleton;
    public static bool RestartRequested;
    [STAThread]
    public static int Main(string[] args)
    {
        Args = args; try
        {
            if (args.Contains("--instance-lock-probe")) { using var probe = new SingleInstance(Option("--probe-name") ?? ""); if (!probe.Owned) return 2; Console.WriteLine("ACQUIRED"); Thread.Sleep(int.Parse(Option("--instance-lock-probe") ?? "1000")); return 0; }
            if(Option("--coverage-probe") is string coverage){var r=System.Text.Json.JsonSerializer.Deserialize<int[]>(coverage)!;Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{reason=DisplayTiming.CoverageReason(new Avalonia.PixelRect(r[0],r[1],r[2],r[3]),!args.Contains("--ignore-fullscreen"),!args.Contains("--ignore-maximized"))}));return 0;}
            if (args.Contains("--disable-autostart")) { new StartupRegistration().SetEnabled(false); return 0; }
#if WINDOWS
            if (args.Contains("--native-draw-self-test"))return NativeDrawSelfTest.Run();
            if (args.Contains("--windows-audio-test"))return WindowsAudioTest.Run();
#endif
            if (args.Contains("--self-test")) return SelfTest.Run();
            int result;
            using(var instance = new SingleInstance(
#if WINDOWS
                args.Contains("--renderer-smoke-test") || args.Contains("--widget-smoke-test") || args.Contains("--windows-video-test") || args.Contains("--winui-test-output") ? "-windows-tests" :
#endif
                "")){
             if (!instance.Owned) { if (!args.Contains("--autostart")) instance.Notify(); return 0; }
             Singleton = instance;if(!args.Contains("--renderer-smoke-test")&&!args.Contains("--widget-smoke-test")&&!args.Contains("--windows-video-test"))DesktopHost.Validate();
             var builder=AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
             var preferences=new Settings();RenderEngines.Configure(Option("--engine")??preferences.WindowsEngine);builder=GraphicsBackend.Configure(builder,Option("--renderer")??(RenderEngines.Native?"DirectX 11":preferences.WindowsRenderer));
             result=builder.StartWithClassicDesktopLifetime(args);
            }
            Singleton=null;
            if(RestartRequested)Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true});
            return result;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    public static string? Option(string key) { int i = Array.IndexOf(Args, key); return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null; }
}
