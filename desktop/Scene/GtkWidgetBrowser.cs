using System.Runtime.InteropServices;
using Avalonia.Threading;
using SkiaSharp;
namespace WirePulse;
internal sealed class GtkWidgetBrowser:IWidgetBrowser {
 const string Gtk="libgtk-3.so.0",Web="libwebkit2gtk-4.1.so.0";readonly IntPtr window,view;readonly DispatcherTimer pump;readonly Ready callback;TaskCompletionSource<byte[]?>? pending;bool disposed;string? audio;static readonly HashSet<GtkWidgetBrowser> retiring=[];readonly Permission permission;readonly Policy policy;readonly Download download;readonly string root;readonly ulong downloadSignal;
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Ready(IntPtr source,IntPtr result,IntPtr data);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate bool Permission(IntPtr view,IntPtr request,IntPtr data);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate bool Policy(IntPtr view,IntPtr decision,int type,IntPtr data);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Download(IntPtr context,IntPtr download,IntPtr data);
 [DllImport("libgobject-2.0.so.0",CharSet=CharSet.Ansi)]static extern ulong g_signal_connect_data(IntPtr instance,string signal,IntPtr callback,IntPtr data,IntPtr destroy,int flags);
 [DllImport(Web)]static extern void webkit_permission_request_deny(IntPtr request);
 [DllImport(Web)]static extern void webkit_policy_decision_ignore(IntPtr decision);
 [DllImport(Web)]static extern IntPtr webkit_navigation_policy_decision_get_request(IntPtr decision);
 [DllImport(Web)]static extern IntPtr webkit_uri_request_get_uri(IntPtr request);
 [DllImport(Web)]static extern IntPtr webkit_web_view_get_context(IntPtr view);
 [DllImport("libgobject-2.0.so.0")]static extern void g_signal_handler_disconnect(IntPtr instance,ulong id);
 [DllImport(Web)]static extern void webkit_download_cancel(IntPtr download);
 [DllImport(Web)]static extern IntPtr webkit_web_view_get_settings(IntPtr view);
 [DllImport(Web)]static extern void webkit_settings_set_allow_file_access_from_file_urls(IntPtr settings,bool value);
 [DllImport(Web)]static extern void webkit_settings_set_allow_universal_access_from_file_urls(IntPtr settings,bool value);
 [DllImport(Gtk)]static extern bool gtk_init_check(IntPtr argc,IntPtr argv);
 [DllImport(Gtk)]static extern IntPtr gtk_offscreen_window_new();
 [DllImport(Gtk)]static extern void gtk_container_add(IntPtr parent,IntPtr child);
 [DllImport(Gtk)]static extern void gtk_widget_set_size_request(IntPtr widget,int width,int height);
 [DllImport(Gtk)]static extern void gtk_window_resize(IntPtr widget,int width,int height);
 [DllImport(Gtk)]static extern void gtk_widget_show_all(IntPtr widget);
 [DllImport(Gtk)]static extern void gtk_widget_destroy(IntPtr widget);
 [DllImport("libglib-2.0.so.0")]static extern bool g_main_context_iteration(IntPtr context,bool block);
 [StructLayout(LayoutKind.Sequential)]struct Rgba{public double Red,Green,Blue,Alpha;}
 [DllImport(Web)]static extern void webkit_web_view_set_background_color(IntPtr view,ref Rgba color);
 [DllImport(Web)]static extern IntPtr webkit_web_view_new();
 [DllImport(Web)]static extern void webkit_web_view_set_is_muted(IntPtr view,bool muted);
 [DllImport(Web,CharSet=CharSet.Ansi)]static extern void webkit_web_view_load_uri(IntPtr view,string uri);
 [DllImport(Web)]static extern void webkit_web_view_get_snapshot(IntPtr view,int region,int options,IntPtr cancellable,Ready ready,IntPtr data);
 [DllImport(Web)]static extern IntPtr webkit_web_view_get_snapshot_finish(IntPtr view,IntPtr result,out IntPtr error);
 [DllImport(Web,CharSet=CharSet.Ansi)]static extern void webkit_web_view_evaluate_javascript(IntPtr view,string script,long length,string? world,string? uri,IntPtr cancellable,IntPtr callback,IntPtr data);
 [DllImport("libcairo.so.2")]static extern IntPtr cairo_image_surface_get_data(IntPtr surface);
 [DllImport("libcairo.so.2")]static extern int cairo_image_surface_get_width(IntPtr surface);
 [DllImport("libcairo.so.2")]static extern int cairo_image_surface_get_height(IntPtr surface);
 [DllImport("libcairo.so.2")]static extern int cairo_image_surface_get_stride(IntPtr surface);
 [DllImport("libcairo.so.2")]static extern void cairo_surface_flush(IntPtr surface);
 [DllImport("libcairo.so.2")]static extern void cairo_surface_destroy(IntPtr surface);
 [DllImport("libglib-2.0.so.0")]static extern void g_error_free(IntPtr error);
 public GtkWidgetBrowser(string path){root=Path.GetDirectoryName(Path.GetFullPath(path))!+Path.DirectorySeparatorChar;if(!gtk_init_check(IntPtr.Zero,IntPtr.Zero))throw new InvalidOperationException("No se pudo iniciar WebKitGTK.");window=gtk_offscreen_window_new();view=webkit_web_view_new();var transparent=new Rgba();webkit_web_view_set_background_color(view,ref transparent);webkit_web_view_set_is_muted(view,true);var settings=webkit_web_view_get_settings(view);webkit_settings_set_allow_file_access_from_file_urls(settings,false);webkit_settings_set_allow_universal_access_from_file_urls(settings,false);
 permission=(_,request,_)=>{webkit_permission_request_deny(request);return true;};policy=(_,decision,type,_)=>{if(type!=0)return false;string uri=Marshal.PtrToStringUTF8(webkit_uri_request_get_uri(webkit_navigation_policy_decision_get_request(decision)))??"";if(Uri.TryCreate(uri,UriKind.Absolute,out var url)&&url.IsFile&&Path.GetFullPath(url.LocalPath).StartsWith(root,StringComparison.Ordinal))return false;webkit_policy_decision_ignore(decision);return true;};download=(_,item,_)=>webkit_download_cancel(item);g_signal_connect_data(view,"permission-request",Marshal.GetFunctionPointerForDelegate(permission),IntPtr.Zero,IntPtr.Zero,0);g_signal_connect_data(view,"decide-policy",Marshal.GetFunctionPointerForDelegate(policy),IntPtr.Zero,IntPtr.Zero,0);downloadSignal=g_signal_connect_data(webkit_web_view_get_context(view),"download-started",Marshal.GetFunctionPointerForDelegate(download),IntPtr.Zero,IntPtr.Zero,0);gtk_container_add(window,view);gtk_widget_set_size_request(view,400,240);gtk_widget_show_all(window);webkit_web_view_load_uri(view,new Uri(Path.GetFullPath(path)).AbsoluteUri);callback=SnapshotReady;pump=new DispatcherTimer(TimeSpan.FromMilliseconds(16),DispatcherPriority.Background,(_,_)=>{for(int i=0;i<32&&g_main_context_iteration(IntPtr.Zero,false);i++){};});pump.Start();}
 public Task<byte[]?> Snapshot(int width,int height){if(disposed)return Task.FromResult<byte[]?>(null);if(pending!=null)return pending.Task;gtk_widget_set_size_request(view,width,height);gtk_window_resize(window,width,height);if(audio!=null){webkit_web_view_evaluate_javascript(view,"window.wirepulseAudio="+audio+";window.dispatchEvent(new CustomEvent('wirepulse:audio',{detail:window.wirepulseAudio}));",-1,null,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);audio=null;}pending=new(TaskCreationOptions.RunContinuationsAsynchronously);webkit_web_view_get_snapshot(view,0,0,IntPtr.Zero,callback,IntPtr.Zero);return pending.Task;}
 void SnapshotReady(IntPtr source,IntPtr result,IntPtr data){IntPtr surface=webkit_web_view_get_snapshot_finish(source,result,out var error);var completion=pending;pending=null;if(surface==IntPtr.Zero){if(error!=IntPtr.Zero)g_error_free(error);completion?.TrySetResult(null);if(disposed)Destroy();return;}try{cairo_surface_flush(surface);int w=cairo_image_surface_get_width(surface),h=cairo_image_surface_get_height(surface),stride=cairo_image_surface_get_stride(surface);using var image=SKImage.FromPixelCopy(new SKImageInfo(w,h,SKColorType.Bgra8888,SKAlphaType.Premul),cairo_image_surface_get_data(surface),stride);using var encoded=image.Encode(SKEncodedImageFormat.Png,90);completion?.TrySetResult(encoded.ToArray());}catch(Exception e){completion?.TrySetException(e);}finally{cairo_surface_destroy(surface);if(disposed)Destroy();}}
 public void Audio(string json){audio=json;}
 public void Dispose(){if(disposed)return;disposed=true;if(pending!=null){retiring.Add(this);return;}Destroy();}
 void Destroy(){pump.Stop();g_signal_handler_disconnect(webkit_web_view_get_context(view),downloadSignal);gtk_widget_destroy(window);retiring.Remove(this);}
}
