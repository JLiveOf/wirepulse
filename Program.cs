using Avalonia;
namespace WirePulse;
internal static class Program {
 public static string[] Args=[];
 [STAThread] public static int Main(string[] args){Args=args;try{if(args.Contains("--self-test"))return SelfTest.Run();DesktopHost.Validate();return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace().StartWithClassicDesktopLifetime(args);}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 public static string? Option(string key){int i=Array.IndexOf(Args,key);return i>=0&&i+1<Args.Length?Args[i+1]:null;}
}
