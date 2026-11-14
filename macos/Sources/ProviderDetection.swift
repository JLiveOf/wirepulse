import AppKit
import CoreAudio

struct BrowserAudioSource {
 let name:String
 let bundleID:String
 let scriptable:Bool
 var script:String {
  guard scriptable else{return "return {provider:'\(name)',kind:'browser',title:'Audio de \(name)',artist:'',album:'',playing:true,duration:0,position:0,id:''};"}
  let tab=bundleID=="com.apple.Safari" ? "a.windows()[0].currentTab()":"a.windows()[0].activeTab()"
  let title=bundleID=="com.apple.Safari" ? "t.name()":"t.title()"
  return """
  try{var a=Application('\(bundleID)'),t=\(tab),u=t.url(),title=\(title),host=(u.match(/^https?:\\/\\/([^\\/?#]+)/i)||[])[1]||'';host=host.toLowerCase();var provider='\(name)';
  if(host=='music.youtube.com')provider='YouTube Music';else if(host=='youtube.com'||host.endsWith('.youtube.com')||host=='youtu.be')provider='YouTube';else if(host=='music.apple.com')provider='Apple Music';else if(host=='open.spotify.com')provider='Spotify';else if(host=='soundcloud.com'||host.endsWith('.soundcloud.com'))provider='SoundCloud';else if(host=='listen.tidal.com')provider='TIDAL';else if(host=='www.deezer.com'||host=='deezer.com')provider='Deezer';
  return {provider:provider,kind:'browser',title:title,artist:'\(name)',album:'Pestaña activa',playing:true,duration:0,position:0,id:host};
  }catch(e){return {provider:'\(name)',kind:'browser',title:'Audio de \(name)',artist:'',album:'',playing:true,duration:0,position:0,id:''};}
  """
 }
}
enum ProviderDetection {
 static func activeOutputBundles()->[String]{
  var address=AudioObjectPropertyAddress(mSelector:kAudioHardwarePropertyProcessObjectList,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain),size:UInt32=0
  guard AudioObjectGetPropertyDataSize(AudioObjectID(kAudioObjectSystemObject),&address,0,nil,&size)==noErr else{return []}
  guard size>0 else{return []}
  var ids=[AudioObjectID](repeating:0,count:Int(size)/MemoryLayout<AudioObjectID>.size)
  let result=ids.withUnsafeMutableBytes{AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject),&address,0,nil,&size,$0.baseAddress!)}
  guard result==noErr else{return []};var bundles=[String]()
  for object in ids{
   var active:UInt32=0,n=UInt32(MemoryLayout<UInt32>.size);var property=AudioObjectPropertyAddress(mSelector:kAudioProcessPropertyIsRunningOutput,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
   guard AudioObjectGetPropertyData(object,&property,0,nil,&n,&active)==noErr,active != 0 else{continue}
   property.mSelector=kAudioProcessPropertyBundleID;var value:UnsafeRawPointer?;n=UInt32(MemoryLayout<UnsafeRawPointer?>.size)
   if AudioObjectGetPropertyData(object,&property,0,nil,&n,&value)==noErr,let value=value{bundles.append(Unmanaged<CFString>.fromOpaque(value).takeRetainedValue() as String)}
  };return bundles
 }
 static func browser(from bundles:[String])->BrowserAudioSource?{
  let definitions:[(String,String,Bool)]=[("Safari","com.apple.Safari",true),("Google Chrome","com.google.Chrome",true),("Microsoft Edge","com.microsoft.edgemac",true),("Brave","com.brave.Browser",true),("Firefox","org.mozilla.firefox",false),("Arc","company.thebrowser.Browser",false),("Navegador WebKit","com.apple.WebKit.WebContent",false)]
  let front=NSWorkspace.shared.frontmostApplication?.bundleIdentifier
  let candidates=definitions.filter{d in bundles.contains{$0==d.1 || $0.hasPrefix(d.1+".")}}
  guard let match=candidates.first(where:{$0.1==front}) ?? candidates.first else{
   let apps=NSWorkspace.shared.runningApplications.filter{app in guard app.activationPolicy == .regular,let id=app.bundleIdentifier else{return false};return bundles.contains{$0==id || $0.hasPrefix(id+".")}}
   guard let app=apps.first(where:{$0.bundleIdentifier==front}) ?? apps.first,let id=app.bundleIdentifier else{return nil}
   return BrowserAudioSource(name:app.localizedName ?? "Audio del sistema",bundleID:id,scriptable:false)
  }
  return BrowserAudioSource(name:match.0,bundleID:match.1,scriptable:match.2)
 }
}
