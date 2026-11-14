using Avalonia;
namespace WirePulse;

internal static class Program
{
    public static string[] Args = [];
    public static SingleInstance? Singleton;
    [STAThread]
    public static int Main(string[] args)
    {
        Args = args; try
        {
            if (args.Contains("--instance-lock-probe")) { using var probe = new SingleInstance(Option("--probe-name") ?? ""); if (!probe.Owned) return 2; Console.WriteLine("ACQUIRED"); Thread.Sleep(int.Parse(Option("--instance-lock-probe") ?? "1000")); return 0; }
            if (args.Contains("--self-test")) return SelfTest.Run();
            using var instance = new SingleInstance(); if (!instance.Owned) { instance.Notify(); return 0; }
            Singleton = instance; DesktopHost.Validate(); return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    public static string? Option(string key) { int i = Array.IndexOf(Args, key); return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null; }
}
