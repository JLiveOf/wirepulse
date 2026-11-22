import AppKit
import UniformTypeIdentifiers

struct LayoutPackage:Codable {
 var format="wirepulse-layout"
 var version=1
 var scene=SceneDocument()
 var snap=true
 var background=Background()
 struct Background:Codable{var color="#000000",asset="",dim=0.0}
 static let limit=2*1024*1024*1024
 static func relative(_ name:String)throws->String{guard !name.isEmpty,!name.hasPrefix("/"),!name.contains("\\"),!name.contains(":"),!name.split(separator:"/").contains(where:{$0==".." || $0=="."}) else{throw SceneAssets.failure("Ruta no válida en el layout.")};return name}
 static func run(_ executable:String,_ arguments:[String],directory:URL?=nil)throws{let process=Process();process.executableURL=URL(fileURLWithPath:executable);process.arguments=arguments;process.currentDirectoryURL=directory;process.standardOutput=FileHandle.nullDevice;process.standardError=FileHandle.nullDevice;try process.run();process.waitUntilExit();guard process.terminationStatus==0 else{throw SceneAssets.failure("No se pudo procesar el archivo de layout.")}}
 static func export(to target:URL,config:Configuration,size:CGSize)throws{
  let fm=FileManager.default,stage=fm.temporaryDirectory.appendingPathComponent("wirepulse-layout-"+UUID().uuidString,isDirectory:true),temporary=target.deletingLastPathComponent().appendingPathComponent(UUID().uuidString+".zip")
  try fm.createDirectory(at:stage,withIntermediateDirectories:true);defer{try? fm.removeItem(at:stage);try? fm.removeItem(at:temporary)}
  var package=LayoutPackage(scene:config.scene ?? SceneDocument.migrate(config,size));package.snap=config.editorSnap;package.background.color=config.colors["backgroundcolor"] ?? "#000000";package.background.dim=config.n("dim")
  var included=[String:String](),count=0,total=0
  func copy(_ file:URL,_ name:String)throws{_=try relative(name);let data=try file.resourceValues(forKeys:[.isSymbolicLinkKey,.fileSizeKey]);guard data.isSymbolicLink != true else{throw SceneAssets.failure("No se admiten enlaces en el layout.")};count+=1;total+=data.fileSize ?? 0;guard count<=2048,total<=limit else{throw SceneAssets.failure("Layout demasiado grande (2 GB / 2048 archivos).")};let destination=stage.appendingPathComponent(name);try fm.createDirectory(at:destination.deletingLastPathComponent(),withIntermediateDirectories:true);try fm.copyItem(at:file,to:destination)}
  func include(_ source:String,widget:Bool=false)throws->String{if source.isEmpty{return ""};if let cached=included[source]{return cached};let file=URL(fileURLWithPath:source).resolvingSymlinksInPath(),prefix="assets/"+UUID().uuidString+"/";var result=prefix+file.lastPathComponent
   if widget{let stored=fm.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("WirePulse/Assets").resolvingSymlinksInPath();var root=file.deletingLastPathComponent();if file.path.hasPrefix(stored.path+"/"){let parts=String(file.path.dropFirst(stored.path.count+1)).split(separator:"/").map(String.init);let index=parts.lastIndex(of:"assets").map{$0+2} ?? 1;root=parts.prefix(index).reduce(stored){$0.appendingPathComponent($1)}}
    let files=fm.enumerator(at:root,includingPropertiesForKeys:[.isSymbolicLinkKey,.isDirectoryKey]);while let entry=files?.nextObject() as? URL{let values=try entry.resourceValues(forKeys:[.isSymbolicLinkKey,.isDirectoryKey]);guard values.isSymbolicLink != true else{throw SceneAssets.failure("El widget contiene enlaces.")};if values.isDirectory != true{try copy(entry,prefix+String(entry.resolvingSymlinksInPath().path.dropFirst(root.path.count+1)))}};result=prefix+String(file.path.dropFirst(root.path.count+1))
   }else{try copy(file,result)};included[source]=result;return result}
  for i in package.scene.items.indices{let item=package.scene.items[i];let group=item.kind=="orb" ? "Orbe":item.kind=="spectrum" ? "Espectro":item.kind=="card" ? "Tarjeta":item.kind=="clock" ? "Reloj":["mesh","stars"].contains(item.kind) ? "Fondo animado":"";let keys=Set(controls.filter{$0.group==group && !SceneDocument.layoutKeys.contains($0.id)}.map(\.id));package.scene.items[i].style=item.style.filter{keys.contains($0.key)};package.scene.items[i].asset=["image","widget"].contains(item.kind) ? try include(item.asset,widget:item.kind=="widget"):""}
  package.background.asset=try include(config.backgroundPath);try JSONEncoder().encode(package).write(to:stage.appendingPathComponent("layout.json"));try run("/usr/bin/zip",["-q","-r",temporary.path,"."],directory:stage);try SceneAssets.validateZip(temporary,limit:limit);if fm.fileExists(atPath:target.path){_=try fm.replaceItemAt(target,withItemAt:temporary)}else{try fm.moveItem(at:temporary,to:target)}
 }
 static func load(_ source:URL,destination:URL?=nil)throws->LayoutPackage{
  let fm=FileManager.default,base=destination ?? fm.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("WirePulse/Assets"),folder=base.appendingPathComponent("layout-"+UUID().uuidString,isDirectory:true)
  do{try SceneAssets.validateZip(source,limit:limit);try fm.createDirectory(at:folder,withIntermediateDirectories:true);try run("/usr/bin/unzip",["-qq",source.path,"-d",folder.path]);try SceneAssets.validate(folder,limit:limit)
   let manifest=folder.appendingPathComponent("layout.json");let bytes=try Data(contentsOf:manifest);guard bytes.count<=2*1024*1024 else{throw SceneAssets.failure("Manifiesto demasiado grande.")};var package=try JSONDecoder().decode(LayoutPackage.self,from:bytes);guard package.format=="wirepulse-layout",package.version==1,package.scene.items.count<=256 else{throw SceneAssets.failure("Versión de layout no compatible.")}
   func resolve(_ name:String)throws->String{if name.isEmpty{return ""};_=try relative(name);let file=folder.appendingPathComponent(name);guard name.hasPrefix("assets/"),fm.fileExists(atPath:file.path) else{throw SceneAssets.failure("Falta un recurso del layout.")};return file.path}
   var ids=Set<String>();for i in package.scene.items.indices{var item=package.scene.items[i];guard SceneDocument.kinds.contains(where:{$0.0==item.kind}),[item.x,item.y,item.width,item.height,item.rotation,item.opacity].allSatisfy(\.isFinite),item.width>0,item.height>0 else{throw SceneAssets.failure("Objeto de layout no válido.")};if !ids.insert(item.id).inserted{item.id=UUID().uuidString};item.x=min(4,max(-4,item.x));item.y=min(4,max(-4,item.y));item.width=min(5,max(0.005,item.width));item.height=min(5,max(0.005,item.height));item.opacity=min(1,max(0,item.opacity));item.rotation=item.rotation.truncatingRemainder(dividingBy:360);item.asset=["image","widget"].contains(item.kind) ? try resolve(item.asset):""
    for spec in controls{guard let value=item.style[spec.id] else{continue};switch spec.kind{case .slider(let lo,let hi,_):guard case .number(let n)=value,n.isFinite else{throw SceneAssets.failure("Propiedad numérica inválida.")};item.style[spec.id] = .number(min(hi,max(lo,n)));default:break}};package.scene.items[i]=item
   };package.background.asset=try resolve(package.background.asset);guard package.background.dim.isFinite else{throw SceneAssets.failure("Oscurecimiento no válido.")};package.background.dim=min(100,max(0,package.background.dim));return package
  }catch{try? fm.removeItem(at:folder);throw error}
 }
 func apply(_ config:Configuration){config.scene=scene;config.editorSnap=snap;config.colors["backgroundcolor"]=background.color;config.backgroundPath=background.asset;config.values["dim"]=background.dim}
}

extension Configuration {
 func transferLayout(importing:Bool){
  guard !layoutTransferRunning else{return}
  if NSApp.windows.contains(where:{$0 is SceneEditorWindow && $0.isVisible}){let alert=NSAlert();alert.messageText=L10n.text("Guarda o cancela el editor antes de transferir un layout.");alert.runModal();return}
  let target:URL
  if importing{let panel=NSOpenPanel();panel.title=L10n.text("Importar layout de WirePulse");panel.allowedContentTypes=[.data];guard panel.runModal() == .OK,let url=panel.url else{return};target=url}
  else{let panel=NSSavePanel();panel.title=L10n.text("Exportar layout de WirePulse");panel.nameFieldStringValue="WirePulse-layout.wirepulse";guard panel.runModal() == .OK,let url=panel.url else{return};target=url}
  layoutTransferRunning=true
  let copy=preview(),size=NSScreen.screens.first(where:{storagePrefix=="display-"+displayKey($0)+"-"})?.frame.size ?? NSScreen.main?.frame.size ?? CGSize(width:1920,height:1080)
  DispatchQueue.global(qos:.userInitiated).async{[weak self] in defer{DispatchQueue.main.async{self?.layoutTransferRunning=false}};do{if importing{let package=try LayoutPackage.load(target);DispatchQueue.main.async{guard let self=self else{return};package.apply(self)}}else{try LayoutPackage.export(to:target,config:copy,size:size)}}catch{DispatchQueue.main.async{NSAlert(error:L10n.error(error)).runModal()}}}
 }
}
