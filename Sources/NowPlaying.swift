import AppKit
import SwiftUI

final class NowPlaying:ObservableObject {
 @Published var provider="Audio del sistema"
 @Published var title=""
 @Published var artist=""
 @Published var album=""
 @Published var playing=false
 @Published var duration=0.0
 @Published var position=0.0
 @Published var artwork:NSImage?
 @Published var artworkState="Sin portada"
 @Published var note="Selecciona Apple Music o Spotify para mostrar sus metadatos."
 private var updated=ProcessInfo.processInfo.systemUptime,busy=false,artBusy=false
 private var activeSource="",identity="",lastArtAttempt=0.0
 private let cache=NSCache<NSString,NSImage>()
 func elapsed()->Double{min(duration,max(0,position+(playing ? ProcessInfo.processInfo.systemUptime-updated:0)))}
 func poll(source:String){
  guard ["Ninguno","Apple Music","Spotify","Automático"].contains(source) else{return}
  activeSource=source
  guard source != "Ninguno" else{title="";artist="";album="";identity="";provider="Audio del sistema";artwork=nil;artworkState="Sin fuente seleccionada";return}
  guard !busy else{return};busy=true
  let browser=source=="Automático" ? ProviderDetection.browser(from:ProviderDetection.activeOutputBundles()):nil
  let script="""
  JSON.stringify((function(){
   function read(fn,f){try{return fn();}catch(e){return f;}}
   function player(kind){try{var a=Application(kind=='Spotify'?'Spotify':'Music');if(!a.running())return null;var t=a.currentTrack(),title=read(function(){return t.name();},'');if(!title)return null;return {provider:kind,kind:'player',title:title,artist:read(function(){return t.artist();},''),album:read(function(){return t.album();},''),id:read(function(){return kind=='Spotify'?t.id():t.persistentID();},''),duration:read(function(){return t.duration()/(kind=='Spotify'?1000:1);},0),position:read(function(){return a.playerPosition();},0),playing:read(function(){return a.playerState()=='playing';},false),art:kind=='Spotify'?read(function(){return t.artworkUrl();},''):''};}catch(e){return null;}}
   function browser(){\(browser?.script ?? "return null;")}
   if('\(source)'!='Automático')return player('\(source)')||{};
   var candidates=[player('Apple Music'),player('Spotify')].filter(function(p){return p!=null;}),playing=candidates.filter(function(p){return p.playing;});
   if(playing.length)return playing[0];var web=browser();if(web)return web;return candidates[0]||{};
  })())
  """
  DispatchQueue.global(qos:.utility).async{[weak self] in
   let (data,error)=Self.run(arguments:["-l","JavaScript","-e",script]);let info=data.flatMap{try? JSONSerialization.jsonObject(with:$0) as? [String:Any]}
   DispatchQueue.main.async{guard let self=self else{return};self.busy=false;guard self.activeSource==source else{return}
    guard let info=info,error.isEmpty else{self.note="Autoriza WirePulse en Privacidad y seguridad → Automatización para leer \(source).";return}
    let next=info["title"] as? String ?? "",artist=info["artist"] as? String ?? "",album=info["album"] as? String ?? "",pid=info["id"] as? String ?? ""
    let resolved=info["provider"] as? String ?? "Audio del sistema",isBrowser=info["kind"] as? String=="browser",spotify=resolved=="Spotify" && !isBrowser
    self.provider=resolved
    let key=resolved+"|"+pid+"|"+next+"|"+artist+"|"+album,changed=key != self.identity
    self.title=next;self.artist=artist;self.album=album;self.duration=info["duration"] as? Double ?? 0;self.position=info["position"] as? Double ?? 0;self.playing=info["playing"] as? Bool ?? false;self.updated=ProcessInfo.processInfo.systemUptime;self.note=next.isEmpty ? "\(source) no tiene una canción activa.":"Leyendo \(resolved)"
    if changed{self.identity=key;self.artwork=self.cache.object(forKey:key as NSString);self.lastArtAttempt=0;self.artworkState=self.artwork==nil ? "Esperando portada":"Portada en caché"}
    if isBrowser{self.artworkState="Sin portada proporcionada por esta aplicación"}
    guard !isBrowser,!next.isEmpty,self.artwork==nil,!self.artBusy,ProcessInfo.processInfo.systemUptime-self.lastArtAttempt>=6 else{return}
    self.artBusy=true;self.lastArtAttempt=ProcessInfo.processInfo.systemUptime;self.artworkState="Leyendo portada de \(resolved)…"
    if spotify,let address=info["art"] as? String,let url=URL(string:address),url.scheme=="https"{
     URLSession.shared.dataTask(with:url){[weak self] bytes,_,_ in let image=bytes.flatMap{NSImage(data:$0)};DispatchQueue.main.async{self?.finishArtwork(image,key:key,message:"Spotify aún no devuelve una portada válida")}}.resume()
    }else if !spotify{
     let executable=Bundle.main.executableURL!
     DispatchQueue.global(qos:.utility).async{[weak self] in let (bytes,error)=Self.run(arguments:["--music-artwork",pid],executable:executable);let image=bytes.flatMap{NSImage(data:$0)};DispatchQueue.main.async{self?.finishArtwork(image,key:key,message:error.isEmpty ? "Apple Music aún no ha cargado la portada":error)}}
    }else{self.artBusy=false;self.artworkState="Spotify no ha proporcionado una URL de portada"}
   }
  }
 }
 private func finishArtwork(_ image:NSImage?,key:String,message:String){
  artBusy=false;guard identity==key else{return}
  if let image=image,image.isValid{artwork=image;cache.setObject(image,forKey:key as NSString);artworkState="Portada cargada"}else{artworkState=message}
 }
 private static func run(arguments:[String],executable:URL=URL(fileURLWithPath:"/usr/bin/osascript"))->(Data?,String){
  let p=Process();p.executableURL=executable;p.arguments=arguments;let output=Pipe(),error=Pipe();p.standardOutput=output;p.standardError=error
  do{try p.run();let bytes=output.fileHandleForReading.readDataToEndOfFile();p.waitUntilExit();let message=error.fileHandleForReading.readDataToEndOfFile();return p.terminationStatus==0 ? (bytes,""):(nil,String(data:message,encoding:.utf8) ?? "Error de automatización")}
  catch{return(nil,error.localizedDescription)}
 }
}

struct MusicCard:View {
 @ObservedObject var config:Configuration;@ObservedObject var media:NowPlaying;@ObservedObject var audio:SystemAudio
 private func stamp(_ t:Double)->String{let s=max(0,Int(t));return String(format:"%d:%02d",s/60,s%60)}
 var body:some View{
  GeometryReader{g in
   if config.on("card") && (!config.on("hideidle") || !media.title.isEmpty){
    let h=g.size.height,cover=max(56,h*0.10),width=min(850,max(260,g.size.width*0.44))
    VStack(spacing:h*0.015){
     HStack(spacing:g.size.width*0.013){
      Group{if let image=media.artwork{Image(nsImage:image).resizable().aspectRatio(contentMode:.fill)}else{Image(systemName:"music.note").font(.system(size:cover*0.45)).frame(maxWidth:.infinity,maxHeight:.infinity).background(Color.white.opacity(0.12))}}
       .frame(width:cover,height:cover).clipped().clipShape(RoundedRectangle(cornerRadius:h*0.02))
      VStack(alignment:.leading,spacing:5){
       Text(media.title.isEmpty ? "Audio del sistema":media.title).font(.system(size:max(15,h*0.027),weight:.bold)).lineLimit(1)
       Text(media.title.isEmpty ? "WirePulse para macOS":media.artist).font(.system(size:max(12,h*0.0225))).foregroundStyle(.white.opacity(0.8)).lineLimit(1)
       Text(media.title.isEmpty ? (audio.running ? "Orbe y LED reactivos al audio real":audio.status):media.album).font(.system(size:max(10,h*0.019))).foregroundStyle(.white.opacity(0.6)).lineLimit(1)
      }.frame(maxWidth:.infinity,alignment:.leading)
      Text(media.title.isEmpty ? "Audio del sistema":media.provider).font(.system(size:max(9,h*0.016))).padding(6).background(.white.opacity(0.12),in:RoundedRectangle(cornerRadius:7))
     }
     TimelineView(.periodic(from:.now,by:1)){_ in VStack(spacing:7){
      GeometryReader{p in ZStack(alignment:.leading){Capsule().fill(.white.opacity(0.18));Capsule().fill(Color(nsColor:config.color("accent"))).frame(width:p.size.width*(media.duration>0 ? media.elapsed()/media.duration:0))}}.frame(height:max(3,h*0.0065))
      HStack{Text(stamp(media.elapsed()));Spacer();Text(media.duration>0 ? "−"+stamp(media.duration-media.elapsed()):"—")}.font(.system(size:max(10,h*0.019))).foregroundStyle(.white.opacity(0.6))
     }}
    }.padding(.horizontal,max(16,g.size.width*0.015)).padding(.vertical,h*0.02).frame(width:width)
     .background(.ultraThinMaterial,in:RoundedRectangle(cornerRadius:h*0.033))
     .overlay(RoundedRectangle(cornerRadius:h*0.033).stroke(.white.opacity(0.25),lineWidth:1))
     .foregroundStyle(.white).environment(\.colorScheme,.dark)
     .scaleEffect(config.n("cardscale"),anchor:.bottomLeading)
     .frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.bottomLeading)
     .padding(.leading,g.size.width*config.n("cardX")/100).padding(.bottom,h*config.n("cardY")/100)
   }
  }.allowsHitTesting(false)
 }
}
