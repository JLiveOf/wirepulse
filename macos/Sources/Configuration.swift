import AppKit
import SwiftUI

struct ControlSpec: Identifiable {
 enum Kind { case toggle, slider(Double,Double,Double), color, image }
 let id: String; let title: String; let group: String; let kind: Kind
}
let controls: [ControlSpec] = [
 .init(id:"backgroundcolor",title:"Color de fondo",group:"Fondo",kind:.color),
 .init(id:"backgroundimage",title:"Imagen de fondo",group:"Fondo",kind:.image),
 .init(id:"dim",title:"Oscurecer imagen",group:"Fondo",kind:.slider(0,100,1)),
 .init(id:"sensitivity",title:"Sensibilidad",group:"Audio",kind:.slider(0,6,0.1)),
 .init(id:"bass",title:"Refuerzo de graves (20–160 Hz)",group:"Audio",kind:.slider(0,4,0.1)),
 .init(id:"gate",title:"Umbral de ruido",group:"Audio",kind:.slider(0,0.2,0.005)),
 .init(id:"smoothing",title:"Suavizado",group:"Audio",kind:.slider(0,0.98,0.01)),
 .init(id:"sphere",title:"Mostrar orbe",group:"Orbe",kind:.toggle),
 .init(id:"fullmesh",title:"Malla completa",group:"Orbe",kind:.toggle),
 .init(id:"wirecolor",title:"Color de la malla",group:"Orbe",kind:.color),
 .init(id:"radius",title:"Radio (% de altura)",group:"Orbe",kind:.slider(10,100,1)),
 .init(id:"sphereX",title:"Posición horizontal",group:"Orbe",kind:.slider(0,120,1)),
 .init(id:"sphereY",title:"Posición vertical",group:"Orbe",kind:.slider(0,140,1)),
 .init(id:"deformation",title:"Deformación por audio",group:"Orbe",kind:.slider(0,2,0.05)),
 .init(id:"pulse",title:"Pulso de graves",group:"Orbe",kind:.slider(0,0.6,0.01)),
 .init(id:"rotation",title:"Velocidad de rotación",group:"Orbe",kind:.slider(0,1,0.01)),
 .init(id:"density",title:"Densidad de malla",group:"Orbe",kind:.slider(12,72,1)),
 .init(id:"linewidth",title:"Grosor de líneas",group:"Orbe",kind:.slider(0.25,2,0.05)),
 .init(id:"glow",title:"Resplandor",group:"Orbe",kind:.slider(0,15,1)),
 .init(id:"opacity",title:"Opacidad de malla",group:"Orbe",kind:.slider(0.1,1,0.05)),
 .init(id:"spectrum",title:"Mostrar espectro",group:"Espectro",kind:.toggle),
 .init(id:"spectrumX",title:"Posición horizontal",group:"Espectro",kind:.slider(0,90,1)),
 .init(id:"spectrumY",title:"Posición vertical",group:"Espectro",kind:.slider(0,90,1)),
 .init(id:"spectrumHeight",title:"Altura",group:"Espectro",kind:.slider(10,100,1)),
 .init(id:"spectrumWidth",title:"Anchura máxima",group:"Espectro",kind:.slider(3,40,1)),
 .init(id:"segmented",title:"LED segmentados",group:"Espectro",kind:.toggle),
 .init(id:"card",title:"Mostrar tarjeta musical",group:"Tarjeta",kind:.toggle),
 .init(id:"hideidle",title:"Ocultar sin canción",group:"Tarjeta",kind:.toggle),
 .init(id:"cardX",title:"Posición horizontal",group:"Tarjeta",kind:.slider(0,90,0.1)),
 .init(id:"cardY",title:"Margen inferior",group:"Tarjeta",kind:.slider(0,90,0.1)),
 .init(id:"cardscale",title:"Escala",group:"Tarjeta",kind:.slider(0.5,1.5,0.05)),
 .init(id:"accent",title:"Color del progreso",group:"Tarjeta",kind:.color),
 .init(id:"unlimitedfps",title:"Usar frecuencia de la pantalla",group:"Rendimiento",kind:.toggle),
 .init(id:"fps",title:"Límite de FPS",group:"Rendimiento",kind:.slider(15,120,1)),
 .init(id:"quality",title:"Resolución de dibujo",group:"Rendimiento",kind:.slider(0.5,1.5,0.25))
]
final class Configuration: ObservableObject {
 let storagePrefix:String
 @Published var independentDisplays:Bool { didSet{UserDefaults.standard.set(independentDisplays,forKey:"independentDisplays")} }
 static let defaults: [String:Double] = ["dim":0,"sensitivity":0.7,"bass":1.2,"gate":0.015,"smoothing":0.5,"sphere":1,"fullmesh":1,"radius":55,"sphereX":97,"sphereY":123,"deformation":1,"pulse":0.2,"rotation":0.5,"density":42,"linewidth":0.65,"glow":6,"opacity":1,"spectrum":1,"spectrumX":0,"spectrumY":0,"spectrumHeight":100,"spectrumWidth":15,"segmented":1,"card":1,"hideidle":0,"cardX":0.8,"cardY":1.3,"cardscale":0.75,"unlimitedfps":0,"fps":60,"quality":1]
 @Published var values: [String:Double] { didSet { UserDefaults.standard.set(values,forKey:storagePrefix+"values") } }
 @Published var colors: [String:String] { didSet { UserDefaults.standard.set(colors,forKey:storagePrefix+"colors") } }
 @Published var backgroundPath: String { didSet { UserDefaults.standard.set(backgroundPath,forKey:storagePrefix+"backgroundPath") } }
 @Published var mediaSource: String { didSet { UserDefaults.standard.set(mediaSource,forKey:"mediaSource") } }
 @Published var topCentered:Bool { didSet{UserDefaults.standard.set(topCentered,forKey:storagePrefix+"topCentered")} }
 @Published var topMargin:Double { didSet{UserDefaults.standard.set(topMargin,forKey:storagePrefix+"topMargin")} }
 @Published var ciderURL:String { didSet{UserDefaults.standard.set(ciderURL,forKey:"ciderURL")} }
 @Published var ciderToken:String { didSet{CiderReader.saveToken(ciderToken)} }
 @Published var paused = false
 init(profile:String="",template:Configuration?=nil) {
  storagePrefix=profile.isEmpty ? "":"display-"+profile+"-"
  independentDisplays=UserDefaults.standard.bool(forKey:"independentDisplays")
  topCentered=UserDefaults.standard.object(forKey:storagePrefix+"topCentered") as? Bool ?? template?.topCentered ?? true
  topMargin=UserDefaults.standard.object(forKey:storagePrefix+"topMargin") as? Double ?? template?.topMargin ?? 4.8
  ciderURL=UserDefaults.standard.string(forKey:"ciderURL") ?? "http://127.0.0.1:10767"
  ciderToken=CiderReader.loadToken()
  values = (template?.values ?? Self.defaults).merging(UserDefaults.standard.dictionary(forKey:storagePrefix+"values") as? [String:Double] ?? [:]) { _,new in new }
  colors = (template?.colors ?? ["backgroundcolor":"#000000","wirecolor":"#ff143c","accent":"#b226bf"]).merging(UserDefaults.standard.dictionary(forKey:storagePrefix+"colors") as? [String:String] ?? [:]) { _,new in new }
  backgroundPath = UserDefaults.standard.string(forKey:storagePrefix+"backgroundPath") ?? template?.backgroundPath ?? ""
  mediaSource = UserDefaults.standard.string(forKey:"mediaSource") ?? "Automático"
  if profile.isEmpty && UserDefaults.standard.integer(forKey:"desktopPresetRevision")<2 {
   values["spectrumX"]=0;values["spectrumY"]=0;values["spectrumHeight"]=100;colors["backgroundcolor"]="#000000"
   UserDefaults.standard.set(values,forKey:storagePrefix+"values");UserDefaults.standard.set(colors,forKey:storagePrefix+"colors");UserDefaults.standard.set(2,forKey:"desktopPresetRevision")
  }
 }
 var hasSaved:Bool{UserDefaults.standard.object(forKey:storagePrefix+"values") != nil}
 func copyLayout(from other:Configuration){values=other.values;colors=other.colors;backgroundPath=other.backgroundPath;topCentered=other.topCentered;topMargin=other.topMargin}
 func n(_ key:String)->Double { values[key] ?? Self.defaults[key] ?? 0 }
 func on(_ key:String)->Bool { n(key)>0.5 }
 func color(_ key:String)->NSColor { NSColor(hex:colors[key] ?? "#ffffff") }
 func reset() { values=Self.defaults; colors=["backgroundcolor":"#000000","wirecolor":"#ff143c","accent":"#b226bf"];backgroundPath="";topCentered=true;topMargin=4.8 }
 func chooseImage() {
  let panel=NSOpenPanel();panel.allowedContentTypes=[.image];panel.canChooseDirectories=false
  guard panel.runModal() == .OK,let url=panel.url else{return}
  do {let folder=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("WirePulse",isDirectory:true);try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
   let target=folder.appendingPathComponent("background-"+UUID().uuidString+"."+url.pathExtension);if FileManager.default.fileExists(atPath:target.path){try FileManager.default.removeItem(at:target)};try FileManager.default.copyItem(at:url,to:target);backgroundPath=target.path
  }catch{NSAlert(error:error).runModal()}
 }
}
extension NSColor {
 convenience init(hex:String) { let v=UInt32(hex.trimmingCharacters(in:CharacterSet(charactersIn:"#")),radix:16) ?? 0;self.init(srgbRed:Double((v>>16)&255)/255,green:Double((v>>8)&255)/255,blue:Double(v&255)/255,alpha:1) }
 var rgba: SIMD4<Float> {let c=usingColorSpace(.deviceRGB) ?? self;return SIMD4(Float(c.redComponent),Float(c.greenComponent),Float(c.blueComponent),Float(c.alphaComponent))}
 var hex: String {let c=usingColorSpace(.deviceRGB) ?? self;return String(format:"#%02x%02x%02x",Int(c.redComponent*255),Int(c.greenComponent*255),Int(c.blueComponent*255))}
}
