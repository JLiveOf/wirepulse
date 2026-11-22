import AppKit
import SwiftUI
import ServiceManagement

struct SettingsView:View {
 @ObservedObject var config:Configuration;@ObservedObject var audio:SystemAudio;@ObservedObject var media:NowPlaying
 @ObservedObject var startup:StartupManager
 var playback:Configuration?=nil
 var edit:(Configuration)->Void = {_ in}
 @State var selected="Audio"
 @State var languagePreference=L10n.preference
 let groups=["Audio","Fondo","Fondo animado","Orbe","Espectro","Tarjeta","Reloj","Rendimiento","Inicio"]
 var body:some View{
  NavigationSplitView{
   List(groups,id:\.self,selection:$selected){group in Label(L10n.text(group),systemImage:["Audio":"waveform","Fondo":"photo","Fondo animado":"sparkles","Reloj":"clock","Orbe":"globe","Espectro":"chart.bar","Tarjeta":"music.note","Rendimiento":"speedometer","Inicio":"power"][group]!).tag(group)}.navigationTitle(L10n.text("WirePulse · Yamato")).navigationSplitViewColumnWidth(170)
  } detail: {
   Form{
    Section{Button(L10n.text("Editar escritorio…")){edit(config)};HStack{Button(L10n.text("Importar layout…")){config.transferLayout(importing:true)};Button(L10n.text("Exportar layout…")){config.transferLayout(importing:false)}}.disabled(config.layoutTransferRunning);if config.layoutTransferRunning{ProgressView(L10n.text("Procesando layout…"))};Text(L10n.text("Mueve, redimensiona y añade objetos con el ratón. Las guías se pueden desactivar.")).font(.caption).foregroundStyle(.secondary)}
    if selected=="Inicio"{UpdateSettingsView();Section(L10n.text("Inicio de sesión")){Picker(L10n.text("Idioma"),selection:$languagePreference){Text(L10n.text("Idioma del sistema")).tag("system");Text("Español").tag("es");Text("English").tag("en")}.onChange(of:languagePreference){_,value in L10n.setPreference(value)};Text(L10n.text("Reinicia WirePulse para aplicar el idioma.")).font(.caption);Toggle(L10n.text("Abrir WirePulse al iniciar sesión"),isOn:Binding(get:{startup.enabled},set:{startup.setEnabled($0)}));Text(L10n.text(startup.note)).font(.caption).foregroundStyle(.secondary);Button(L10n.text("Gestionar ítems de inicio")){SMAppService.openSystemSettingsLoginItems()}}}
    if selected=="Fondo animado" && config.scene==nil{Section(L10n.text("Material")){Picker(L10n.text("Tipo"),selection:Binding(get:{Int(config.n("bgstyle"))},set:{config.values["bgstyle"]=Double($0)})){Text(L10n.text("Estrellas")).tag(0);Text(L10n.text("Malla")).tag(1)};TimelineView(.periodic(from:.now,by:1)){_ in let snapshot=audio.analyzer.snapshot(),ciderBpm=media.tempoBpm,bpm=ciderBpm>0 ? ciderBpm:(snapshot.age<2 ? snapshot.bpm:0);Text(L10n.text(bpm>0 ? "\(Int(bpm.rounded())) BPM"+(ciderBpm>0 ? " · Cider":" estimados"):"Esperando un ritmo estable")).font(.caption).foregroundStyle(.secondary)};Text(L10n.text("Sin sonido mantiene la velocidad base. Los beats aceleran el desplazamiento; los BPM se estiman a partir del audio.")).font(.caption).foregroundStyle(.secondary)}}
    if selected=="Reloj" && config.scene==nil{Section(L10n.text("Fuente")){Picker(L10n.text("Fuente del reloj"),selection:$config.clockFont){Text(L10n.text("Sistema")).tag("System");ForEach(EditorChrome.fonts,id:\.self){font in Text(font).tag(font)}}}}
    if selected=="Audio"{
     Section(L10n.text("Audio del sistema")){
      Text(L10n.text(audio.status)).textSelection(.enabled)
      TimelineView(.periodic(from:.now,by:1)){_ in let s=audio.analyzer.snapshot();Text(L10n.text("64 bandas · FFT \(s.fftSize) · \(String(format:"%.2f",s.binWidthHz)) Hz/bin")).font(.caption).foregroundStyle(.secondary)}
      Text(L10n.text("El límite superior es la mitad del muestreo capturado (Nyquist). No cambia la calidad de reproducción ni aumenta la resolución del archivo lossless.")).font(.caption).foregroundStyle(.secondary)
      Text(L10n.text("Analiza el sonido que reproducen las aplicaciones, en memoria. No usa el micrófono ni guarda grabaciones.")).font(.caption).foregroundStyle(.secondary)
      HStack{Button(L10n.text(audio.running ? "Detener captura":audio.starting ? "Cancelar inicio":"Activar audio del sistema")){if audio.running || audio.starting{audio.stop()}else{audio.start()}};Button(L10n.text("Permisos de audio")){NSWorkspace.shared.open(URL(string:"x-apple.systempreferences:com.apple.preference.security?Privacy_AudioCapture")!)}}
      TimelineView(.periodic(from:.now,by:0.1)){_ in let s=audio.analyzer.snapshot();HStack(alignment:.bottom,spacing:2){ForEach(0..<64,id:\.self){i in RoundedRectangle(cornerRadius:2).fill(Color.accentColor.opacity(s.age<0.4 ? 0.85:0.2)).frame(height:max(2,CGFloat(s.age<0.4 ? s.bands[i]:0)*52))}}.frame(height:54).accessibilityLabel(L10n.text("Nivel del audio capturado"))}
     }
    }
    if selected=="Tarjeta"{Section(L10n.text("Canción actual")){
     Picker(L10n.text("Leer metadatos de"),selection:Binding(get:{(playback ?? config).mediaSource},set:{(playback ?? config).mediaSource=$0})){Text(L10n.text("Automático")).tag("Automático");Text(L10n.text("Ninguno")).tag("Ninguno");Text(L10n.text("Apple Music")).tag("Apple Music");Text(L10n.text("Spotify")).tag("Spotify");Text(L10n.text("Cider")).tag("Cider")}
     TextField(L10n.text("Dirección local de Cider"),text:Binding(get:{(playback ?? config).ciderURL},set:{(playback ?? config).ciderURL=$0}))
     SecureField("Token de la API de Cider",text:Binding(get:{(playback ?? config).ciderToken},set:{(playback ?? config).ciderToken=$0}))
     Text(L10n.text("Cider: activa su API local en Connectivity y crea un token de Playback. El token se guarda en el Llavero del Mac.")).font(.caption).foregroundStyle(.secondary)
     Text(L10n.text(media.note)).font(.caption).foregroundStyle(.secondary)
     Text(L10n.text(media.artworkState)).font(.caption).foregroundStyle(.secondary)
     Text(L10n.text("Automático prioriza Apple Music o Spotify en reproducción. Para audio de navegador identifica YouTube y otros servicios a partir de la pestaña activa; con varias pestañas o sin permiso, la identificación puede limitarse al navegador.")).font(.caption).foregroundStyle(.secondary)
     Text(L10n.text("Esta opción solicita permiso de Automatización para consultar canción, portada y progreso. El análisis del sonido funciona con todas las aplicaciones, independientemente de esta selección.")).font(.caption).foregroundStyle(.secondary)
    }}
    Section(selected){ForEach(controls.filter{$0.group==selected && !SceneDocument.layoutKeys.contains($0.id) && (config.scene==nil || ["Audio","Fondo","Rendimiento"].contains(selected))}){spec in row(spec)}}
    if selected=="Rendimiento"{Section{
     Picker(L10n.text("Perfil de rendimiento"),selection:$config.renderProfile){Text(L10n.text("Automático")).tag("Automático");Text(L10n.text("Calidad")).tag("Calidad");Text(L10n.text("Ligero")).tag("Ligero")}
     Text(L10n.text("Ligero limita el detalle, elimina el resplandor y utiliza hasta 60 FPS.")).font(.caption).foregroundStyle(.secondary)
     Toggle(L10n.text("Pausar la animación"),isOn:$config.paused)
     Text(L10n.text("Se detiene durante el reposo o el bloqueo de sesión. La frecuencia máxima respeta la de cada pantalla.")).font(.caption).foregroundStyle(.secondary)
    }}
    Section{HStack{Button(L10n.text("Restablecer valores")){config.reset()};Spacer();Text(L10n.text("Swift · Metal · Core Audio")).font(.caption).foregroundStyle(.secondary)}}
   }.formStyle(.grouped).navigationTitle(L10n.text(selected))
  }.frame(minWidth:760,minHeight:620)
 }
 @ViewBuilder private func row(_ spec:ControlSpec)->some View{
  switch spec.kind{
  case .toggle:Toggle(L10n.text(spec.title),isOn:Binding(get:{config.on(spec.id)},set:{config.values[spec.id]=$0 ? 1:0}))
  case let .slider(lo,hi,step):VStack(alignment:.leading){HStack{Text(L10n.text(spec.title));Spacer();Text(String(format:step<0.01 ? "%.3f":step<1 ? "%.2f":"%.0f",config.n(spec.id))).foregroundStyle(.secondary).monospacedDigit()};Slider(value:Binding(get:{config.n(spec.id)},set:{config.values[spec.id]=$0}),in:lo...hi,step:step)}
  case .color:ColorPropertyView(title:spec.title,color:Binding(get:{config.colors[spec.id] ?? "#ffffff"},set:{config.colors[spec.id]=$0}),gradient:Binding(get:{config.gradient(spec.id)},set:{config.gradients[spec.id]=$0}),supportsOpacity:false)
  case .image:HStack{Text(L10n.text(spec.title));Spacer();Button(L10n.text("Elegir…")){config.chooseImage()};if !config.backgroundPath.isEmpty{Button(L10n.text("Quitar")){config.backgroundPath=""}}}
  }
 }
}

func displayKey(_ screen:NSScreen)->String{let id=(screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.uint32Value ?? 0;if let uuid=CGDisplayCreateUUIDFromDisplayID(id)?.takeRetainedValue(){return CFUUIDCreateString(nil,uuid) as String};return String(id)}
struct DisplaySettingsView:View{
 @ObservedObject var root:Configuration;@ObservedObject var audio:SystemAudio;@ObservedObject var media:NowPlaying;let profiles:[String:Configuration];@ObservedObject var startup:StartupManager
 var edit:(Configuration)->Void = {_ in}
 @State private var selected=""
 var body:some View{VStack(spacing:0){HStack{Toggle(L10n.text("Ajustar cada pantalla por separado"),isOn:$root.independentDisplays);Spacer();if root.independentDisplays{Picker(L10n.text("Pantalla"),selection:$selected){ForEach(NSScreen.screens,id:\.self){screen in Text(screen.localizedName).tag(displayKey(screen))}}.frame(width:280)}}.padding();Divider();SettingsView(config:root.independentDisplays ? (profiles[selected] ?? profiles[NSScreen.screens.first.map{displayKey($0)} ?? ""] ?? root):root,audio:audio,media:media,startup:startup,playback:root,edit:edit)}.frame(minWidth:820,minHeight:700).onAppear{if selected.isEmpty{selected=NSScreen.screens.first.map{displayKey($0)} ?? ""}}}
}
