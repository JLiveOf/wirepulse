#if WINDOWS
using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;
namespace WirePulse;
internal sealed class WindowsWidgetBrowser:IWidgetBrowser {
 readonly Task ready;CoreWebView2Controller? controller;readonly IntPtr host;bool loaded,disposed;string? audio;
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
 [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
 public WindowsWidgetBrowser(string path){host=CreateWindowEx(0,"STATIC","WirePulse widget",0,-10000,-10000,400,240,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);ready=Initialize(path);}
 async Task Initialize(string path){try{var options=new CoreWebView2EnvironmentOptions("--disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows --autoplay-policy=user-gesture-required");var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Settings.Folder,"WidgetBrowser"),options);if(disposed)return;controller=await environment.CreateCoreWebView2ControllerAsync(host);if(disposed){controller.Close();return;}controller.IsVisible=true;controller.DefaultBackgroundColor=System.Drawing.Color.Transparent;var web=controller.CoreWebView2;web.Settings.AreHostObjectsAllowed=false;web.Settings.AreDevToolsEnabled=false;web.Settings.AreDefaultContextMenusEnabled=false;web.IsMuted=true;web.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;web.NewWindowRequested+=(_,e)=>e.Handled=true;web.DownloadStarting+=(_,e)=>e.Cancel=true;web.SetVirtualHostNameToFolderMapping("widget.wirepulse.invalid",Path.GetDirectoryName(path)!,CoreWebView2HostResourceAccessKind.DenyCors);web.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith("https://widget.wirepulse.invalid/",StringComparison.OrdinalIgnoreCase)&&e.Uri!="about:blank")e.Cancel=true;};web.NavigationCompleted+=(_,e)=>loaded=e.IsSuccess;web.Navigate("https://widget.wirepulse.invalid/"+Uri.EscapeDataString(Path.GetFileName(path)));}catch(WebView2RuntimeNotFoundException){throw new InvalidOperationException("Instala Microsoft Edge WebView2 Runtime para usar widgets HTML.");}}
 public async Task<byte[]?> Snapshot(int width,int height){await ready;if(disposed||controller==null||!loaded)return null;controller.Bounds=new System.Drawing.Rectangle(0,0,width,height);if(audio!=null){string json=audio;audio=null;await controller.CoreWebView2.ExecuteScriptAsync("window.wirepulseAudio="+json+";window.dispatchEvent(new CustomEvent('wirepulse:audio',{detail:window.wirepulseAudio}));");}using var stream=new MemoryStream();await controller.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);return stream.ToArray();}
 public void Audio(string json){audio=json;}
 public void Dispose(){disposed=true;controller?.Close();DestroyWindow(host);}
}
#endif
