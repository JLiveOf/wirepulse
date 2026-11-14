import AppKit
import ServiceManagement
import SwiftUI

final class StartupManager:ObservableObject {
 @Published private(set) var enabled=false
 @Published private(set) var note=""
 private let defaults:UserDefaults
 private let read:()->SMAppService.Status
 private let register:()throws->Void
 private let unregister:()throws->Void
 init(defaults:UserDefaults = .standard,read:@escaping()->SMAppService.Status={SMAppService.mainApp.status},register:@escaping()throws->Void={try SMAppService.mainApp.register()},unregister:@escaping()throws->Void={try SMAppService.mainApp.unregister()}){self.defaults=defaults;self.read=read;self.register=register;self.unregister=unregister;refresh()}
 static func shouldPrompt(asked:Bool,autostart:Bool)->Bool{!asked && !autostart}
 func refresh(){let state=read();enabled=state == .enabled || state == .requiresApproval;note=state == .requiresApproval ? "Pendiente de autorización en Ajustes del Sistema → Ítems de inicio.":(enabled ? "WirePulse está registrado como ítem de inicio de tu cuenta.":"WirePulse no se abrirá automáticamente.")}
 func setEnabled(_ value:Bool){defaults.set(true,forKey:"startupPrompted");do{if value{if read() != .enabled && read() != .requiresApproval{try register()}}else if read() == .enabled || read() == .requiresApproval{try unregister()};refresh()}catch{refresh();note="No se pudo actualizar el inicio automático: "+error.localizedDescription}}
 func promptIfNeeded(){guard Self.shouldPrompt(asked:defaults.bool(forKey:"startupPrompted"),autostart:CommandLine.arguments.contains("--autostart")) else{return};defaults.set(true,forKey:"startupPrompted");let alert=NSAlert();alert.messageText="¿Abrir WirePulse al iniciar sesión?";alert.informativeText="El fondo se abrirá sin mostrar los ajustes. Puedes cambiar esta opción más adelante en Ajustes → Inicio.";alert.addButton(withTitle:"Sí, abrir al iniciar");alert.addButton(withTitle:"Ahora no");NSApp.activate(ignoringOtherApps:true);setEnabled(alert.runModal() == .alertFirstButtonReturn)}
}
