using Avalonia;
using SkiaSharp;
using System.Runtime.InteropServices;
namespace WirePulse;

/// Process-wide choice. Avalonia creates the graphics device before any windows exist.
public static class GraphicsBackend {
 public static readonly string[] Choices=["Automático","DirectX 11","Vulkan","OpenGL","CPU (compatibilidad)"];
 public static string Requested{get;private set;}="Automático";
 public static string PlatformBackend{get;private set;}="Sin inicializar";
 static string? driver;
 public static bool Observed{get;private set;}
 public static string Normalize(string? value)=>Choices.Contains(value??"")?value!:"Automático";
 public static string[] FallbackOrder(string? value)=>Normalize(value) switch{
  "Vulkan"=>["Vulkan","DirectX 11","CPU (compatibilidad)"],
  "OpenGL"=>["OpenGL","DirectX 11","CPU (compatibilidad)"],
  "CPU (compatibilidad)"=>["CPU (compatibilidad)"],
  _=>["DirectX 11","CPU (compatibilidad)"]};
 public static string DetectPlatform(string? type)=>type switch{
  null=>"CPU (compatibilidad)",
  var x when x.Contains("Vulkan",StringComparison.OrdinalIgnoreCase)=>"Vulkan",
  var x when (x.Contains("D3D11",StringComparison.OrdinalIgnoreCase)||x.Contains("Angle",StringComparison.OrdinalIgnoreCase)||x.Contains("EglContext",StringComparison.OrdinalIgnoreCase))=>"DirectX 11",
  var x when x.Contains("Wgl",StringComparison.OrdinalIgnoreCase)=>"OpenGL",
  _=>"GPU · Skia"};
 public static void Observe(GRContext? context,string? contextType){PlatformBackend=context==null?"CPU (compatibilidad)":context.Backend==GRBackend.Vulkan?"Vulkan":DetectPlatform(contextType??"UnknownGPU");ObserveDriver(context!=null);Observed=true;}
 public static AppBuilder Configure(AppBuilder builder,string? preference){Requested=Normalize(preference);if(!OperatingSystem.IsWindows())return builder;return builder.With(new Win32PlatformOptions{RenderingMode=FallbackOrder(Requested).Select(x=>x switch{"Vulkan"=>Win32RenderingMode.Vulkan,"OpenGL"=>Win32RenderingMode.Wgl,"CPU (compatibilidad)"=>Win32RenderingMode.Software,_=>Win32RenderingMode.AngleEgl}).ToArray()});}
 public static string Description(bool gpu){if(!gpu)return "CPU · Skia";if(!OperatingSystem.IsWindows())return "GPU · Skia";return driver==null?PlatformBackend+" · Skia":PlatformBackend+" · "+driver;}
 // Called once while Skia's current GL context is leased, never from the UI thread.
 static void ObserveDriver(bool gpu){if(!gpu||driver!=null||!OperatingSystem.IsWindows()||PlatformBackend is not("DirectX 11" or "OpenGL"))return;driver="GPU · Skia";try{string library=PlatformBackend=="DirectX 11"?"av_libglesv2.dll":"opengl32.dll";if(NativeLibrary.TryLoad(library,typeof(AppBuilder).Assembly,null,out var module)){try{if(!NativeLibrary.TryGetExport(module,"glGetString",out var address))return;var get=Marshal.GetDelegateForFunctionPointer<GetString>(address);var text=Marshal.PtrToStringAnsi(get(0x1F01));if(!string.IsNullOrWhiteSpace(text))driver=text.Length>220?text[..220]:text;}finally{NativeLibrary.Free(module);}}}catch{/* Diagnostics must never break a frame. */}}
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate IntPtr GetString(uint name);
}
