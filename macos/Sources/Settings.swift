import AppKit
import SwiftUI
import ServiceManagement

struct SettingsView:View {
 @ObservedObject var config:Configuration;@ObservedObject var audio:SystemAudio;@ObservedObject var media:NowPlaying
 @ObservedObject var startup:StartupManager
 var playback:Configuration?=nil
 @State var selected="Audio"
 let groups=["Audio","Fondo","Orbe","Espectro","Tarjeta","Rendimiento","Inicio"]
 var body:some View{
  NavigationSplitView{
   List(groups,id:\.self,selection:$selected){group in Label(group,systemImage:["Audio":"waveform","Fondo":"photo","Orbe":"globe","Espectro":"chart.bar","Tarjeta":"music.note","Rendimiento":"speedometer","Inicio":"power"][group]!).tag(group)}.navigationTitle("WirePulse").navigationSplitViewColumnWidth(170)
  } detail: {
   Form{
    if selected=="Inicio"{Section("Inicio de sesión"){Toggle("Abrir WirePulse al iniciar sesión",isOn:Binding(get:{startup.enabled},set:{startup.setEnabled($0)}));Text(startup.note).font(.caption).foregroundStyle(.secondary);Button("Gestionar ítems de inicio"){SMAppService.openSystemSettingsLoginItems()}}}
    if selected=="Audio"{
     Section("Audio del sistema"){
      Text(audio.status).textSelection(.enabled)
      TimelineView(.periodic(from:.now,by:1)){_ in let s=audio.analyzer.snapshot();Text("64 bandas · FFT \(s.fftSize) · \(String(format:"%.2f",s.binWidthHz)) Hz/bin").font(.caption).foregroundStyle(.secondary)}
      Text("El límite superior es la mitad del muestreo capturado (Nyquist). No cambia la calidad de reproducción ni aumenta la resolución del archivo lossless.").font(.caption).foregroundStyle(.secondary)
      Text("Analiza el sonido que reproducen las aplicaciones, en memoria. No usa el micrófono ni guarda grabaciones.").font(.caption).foregroundStyle(.secondary)
      HStack{Button(audio.running ? "Detener captura":"Activar audio del sistema"){if audio.running{audio.stop()}else{audio.start()}};Button("Permisos de audio"){NSWorkspace.shared.open(URL(string:"x-apple.systempreferences:com.apple.preference.security?Privacy_AudioCapture")!)}}
      TimelineView(.periodic(from:.now,by:0.1)){_ in let s=audio.analyzer.snapshot();HStack(alignment:.bottom,spacing:2){ForEach(0..<64,id:\.self){i in RoundedRectangle(cornerRadius:2).fill(Color.accentColor.opacity(s.age<0.4 ? 0.85:0.2)).frame(height:max(2,CGFloat(s.age<0.4 ? s.bands[i]:0)*52))}}.frame(height:54).accessibilityLabel("Nivel del audio capturado")}
     }
    }
    if selected=="Tarjeta"{Section("Canción actual"){
     Picker("Leer metadatos de",selection:Binding(get:{(playback ?? config).mediaSource},set:{(playback ?? config).mediaSource=$0})){Text("Automático").tag("Automático");Text("Ninguno").tag("Ninguno");Text("Apple Music").tag("Apple Music");Text("Spotify").tag("Spotify");Text("Cider").tag("Cider")}
     Toggle("Tarjeta arriba centrada",isOn:$config.topCentered)
     Slider(value:$config.topMargin,in:0...50,step:0.1){Text("Margen superior (%)")}
     TextField("Dirección local de Cider",text:Binding(get:{(playback ?? config).ciderURL},set:{(playback ?? config).ciderURL=$0}))
     SecureField("Token de la API de Cider",text:Binding(get:{(playback ?? config).ciderToken},set:{(playback ?? config).ciderToken=$0}))
     Text("Cider: activa su API local en Connectivity y crea un token de Playback. El token se guarda en el Llavero del Mac.").font(.caption).foregroundStyle(.secondary)
     Text(media.note).font(.caption).foregroundStyle(.secondary)
     Text(media.artworkState).font(.caption).foregroundStyle(.secondary)
     Text("Automático prioriza Apple Music o Spotify en reproducción. Para audio de navegador identifica YouTube y otros servicios a partir de la pestaña activa; con varias pestañas o sin permiso, la identificación puede limitarse al navegador.").font(.caption).foregroundStyle(.secondary)
     Text("Esta opción solicita permiso de Automatización para consultar canción, portada y progreso. El análisis del sonido funciona con todas las aplicaciones, independientemente de esta selección.").font(.caption).foregroundStyle(.secondary)
    }}
    Section(selected){ForEach(controls.filter{$0.group==selected}){spec in row(spec)}}
    if selected=="Rendimiento"{Section{
     Toggle("Pausar la animación",isOn:$config.paused)
     Text("Se detiene durante el reposo o el bloqueo de sesión. La frecuencia máxima respeta la de cada pantalla.").font(.caption).foregroundStyle(.secondary)
    }}
    Section{HStack{Button("Restablecer valores"){config.reset()};Spacer();Text("Swift · Metal · Core Audio").font(.caption).foregroundStyle(.secondary)}}
   }.formStyle(.grouped).navigationTitle(selected)
  }.frame(minWidth:760,minHeight:620)
 }
 @ViewBuilder private func row(_ spec:ControlSpec)->some View{
  switch spec.kind{
  case .toggle:Toggle(spec.title,isOn:Binding(get:{config.on(spec.id)},set:{config.values[spec.id]=$0 ? 1:0}))
  case let .slider(lo,hi,step):VStack(alignment:.leading){HStack{Text(spec.title);Spacer();Text(String(format:step<0.01 ? "%.3f":step<1 ? "%.2f":"%.0f",config.n(spec.id))).foregroundStyle(.secondary).monospacedDigit()};Slider(value:Binding(get:{config.n(spec.id)},set:{config.values[spec.id]=$0}),in:lo...hi,step:step)}
  case .color:ColorPicker(spec.title,selection:Binding(get:{Color(nsColor:config.color(spec.id))},set:{config.colors[spec.id]=NSColor($0).hex}),supportsOpacity:false)
  case .image:HStack{Text(spec.title);Spacer();Button("Elegir…"){config.chooseImage()};if !config.backgroundPath.isEmpty{Button("Quitar"){config.backgroundPath=""}}}
  }
 }
}

func displayKey(_ screen:NSScreen)->String{let id=(screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.uint32Value ?? 0;if let uuid=CGDisplayCreateUUIDFromDisplayID(id)?.takeRetainedValue(){return CFUUIDCreateString(nil,uuid) as String};return String(id)}
struct DisplaySettingsView:View{
 @ObservedObject var root:Configuration;@ObservedObject var audio:SystemAudio;@ObservedObject var media:NowPlaying;let profiles:[String:Configuration];@ObservedObject var startup:StartupManager
 @State private var selected=""
 var body:some View{VStack(spacing:0){HStack{Toggle("Ajustar cada pantalla por separado",isOn:$root.independentDisplays);Spacer();if root.independentDisplays{Picker("Pantalla",selection:$selected){ForEach(NSScreen.screens,id:\.self){screen in Text(screen.localizedName).tag(displayKey(screen))}}.frame(width:280)}}.padding();Divider();SettingsView(config:root.independentDisplays ? (profiles[selected] ?? profiles[NSScreen.screens.first.map{displayKey($0)} ?? ""] ?? root):root,audio:audio,media:media,startup:startup,playback:root)}.frame(minWidth:820,minHeight:700).onAppear{if selected.isEmpty{selected=NSScreen.screens.first.map{displayKey($0)} ?? ""}}}
}
