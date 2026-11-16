namespace WirePulse;
public static class RenderEngines {
 public const string Skia="Skia",Direct3D="Direct3D 11 · lotes GPU",Direct2D="Direct2D · vectorial";
 public static string Default=>OperatingSystem.IsWindows()?Direct3D:Skia;
 public static readonly string[] Choices=[Skia,Direct3D,Direct2D];public static string Requested{get;private set;}=Skia;
 public static string Normalize(string? value)=>Choices.Contains(value??"")?value!:Skia;
 public static void Configure(string? value)=>Requested=OperatingSystem.IsWindows()?Normalize(value):Skia;
 public static bool Native=>Requested!=Skia;
}
