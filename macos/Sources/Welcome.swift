import AppKit
import AVFoundation

final class WelcomeController:NSObject,NSWindowDelegate {
 static var active=false
 static let versionKey="welcomeCompletedVersion"
 static func needs(_ version:String,defaults:UserDefaults = .standard)->Bool{defaults.string(forKey:versionKey) != version}
 let window:NSWindow
 private let root=NSView(),logo=NSImageView(),name=NSTextField(labelWithString:"WirePulse"),form=NSStackView()
 private let audio:SystemAudio,startup:StartupManager,onFinish:()->Void
 private var timer:Timer?,player:AVAudioPlayer?,permissionStatus:NSTextField?,setup=false,completed=false
 private var startToggle:NSButton?,updatesToggle:NSButton?,channel:NSPopUpButton?,errorLabel:NSTextField?
 init(audio:SystemAudio,startup:StartupManager,onFinish:@escaping()->Void){
  self.audio=audio;self.startup=startup;self.onFinish=onFinish
  window=NSWindow(contentRect:NSRect(x:0,y:0,width:620,height:560),styleMask:[.titled,.closable],backing:.buffered,defer:false)
  super.init();Self.active=true;window.appearance=NSAppearance(named:.darkAqua);window.title="WirePulse";window.isReleasedWhenClosed=false;window.delegate=self;window.backgroundColor=NSColor(hex:"#101115");window.contentView=root;root.wantsLayer=true
  logo.image=NSImage(named:NSImage.applicationIconName);logo.imageScaling = .scaleProportionallyUpOrDown;root.addSubview(logo);name.font = .systemFont(ofSize:30,weight:.bold);name.textColor = .white;root.addSubview(name)
  form.orientation = .vertical;form.alignment = .leading;form.spacing=14;form.frame=NSRect(x:36,y:28,width:548,height:410);root.addSubview(form)
  permission();window.center();NSApp.activate(ignoringOtherApps:true);window.makeKeyAndOrderFront(nil)
 }
 private func label(_ text:String,_ size:CGFloat=14)->NSTextField{let field=NSTextField(wrappingLabelWithString:L10n.text(text));field.font = .systemFont(ofSize:size);field.textColor = .white;field.preferredMaxLayoutWidth=548;return field}
 private func button(_ title:String,_ action:Selector)->NSButton{let b=NSButton(title:L10n.text(title),target:self,action:action);b.bezelStyle = .rounded;return b}
 private func clearForm(){for view in form.arrangedSubviews{form.removeArrangedSubview(view);view.removeFromSuperview()}}
 private func header(){logo.frame=NSRect(x:28,y:494,width:42,height:42);name.frame=NSRect(x:84,y:497,width:300,height:32);name.font = .systemFont(ofSize:22,weight:.bold)}
 private func permission(){header();clearForm();form.addArrangedSubview(label("Activa el audio del sistema",23));form.addArrangedSubview(label("WirePulse necesita permiso para que el fondo reaccione a tu música. Pulsa Activar audio y acepta el aviso de macOS. No se guarda ni se envía el audio."));let status=label("Esperando permiso de audio…");permissionStatus=status;form.addArrangedSubview(status);form.addArrangedSubview(button("Activar audio",#selector(requestAudio)));form.addArrangedSubview(button("Abrir permisos del sistema",#selector(openPermissions)));let next=button("Continuar",#selector(continueAfterPermission));form.addArrangedSubview(next);timer=Timer.scheduledTimer(withTimeInterval:0.25,repeats:true){[weak self] _ in self?.refreshPermission()};refreshPermission()}
 @objc private func requestAudio(){audio.start()}
 @objc private func openPermissions(){if let url=URL(string:"x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture"){NSWorkspace.shared.open(url)}}
 private func refreshPermission(){
  let ready=audio.running && audio.analyzer.snapshot().frames>0
  permissionStatus?.stringValue=ready ? L10n.text("Audio del sistema preparado."):L10n.text(audio.status)
 }
 @objc private func continueAfterPermission(){timer?.invalidate();splash()}
 func splash(playSound:Bool=true){timer?.invalidate();clearForm();form.isHidden=true;logo.frame=NSRect(x:242,y:284,width:136,height:136);name.font = .systemFont(ofSize:30,weight:.bold);name.frame=NSRect(x:238,y:224,width:240,height:44)
  if playSound,let url=Bundle.main.url(forResource:"Welcome",withExtension:"wav"){do{player=try AVAudioPlayer(contentsOf:url);player?.prepareToPlay();player?.play()}catch{NSLog("Welcome audio: %@",error.localizedDescription)}}
  timer=Timer.scheduledTimer(withTimeInterval:2.9,repeats:false){[weak self] _ in self?.showSetup()}
 }
 func showSetup(){guard !setup else{return};setup=true;timer?.invalidate();player?.stop();player=nil;clearForm();form.isHidden=false;form.alphaValue=0
  form.addArrangedSubview(label("Vamos a preparar tu escritorio",23))
  startup.refresh();let start=NSButton(checkboxWithTitle:L10n.text("Abrir WirePulse al iniciar sesión"),target:nil,action:nil);start.state=startup.enabled ? .on:.off;startToggle=start;form.addArrangedSubview(start)
  let updates=NSButton(checkboxWithTitle:L10n.text("Comprobar actualizaciones automáticamente"),target:nil,action:nil);updates.state=UpdateManager.shared.preferences.automatic ? .on:.off;updatesToggle=updates;form.addArrangedSubview(updates)
  form.addArrangedSubview(label("Canal de actualizaciones"));let picker=NSPopUpButton();picker.addItems(withTitles:[L10n.text("Estable"),L10n.text("Beta (prereleases)")]);picker.selectItem(at:UpdateManager.shared.preferences.channel=="beta" ? 1:0);channel=picker;form.addArrangedSubview(picker)
  form.addArrangedSubview(label("Puedes cambiar estas opciones más adelante en Ajustes."));let error=label("");error.textColor = .systemOrange;errorLabel=error;form.addArrangedSubview(error);let finish=button("Empezar",#selector(save));finish.keyEquivalent="\r";form.addArrangedSubview(finish)
  NSAnimationContext.runAnimationGroup{context in context.duration=0.55;context.timingFunction=CAMediaTimingFunction(name:.easeInEaseOut);logo.animator().frame=NSRect(x:28,y:494,width:42,height:42);name.font = .systemFont(ofSize:22,weight:.bold);name.animator().frame=NSRect(x:84,y:497,width:300,height:32);form.animator().alphaValue=1}
 }
 @objc private func save(){let desired=startToggle?.state == .on;if desired != startup.enabled{startup.setEnabled(desired)};guard startup.enabled==desired else{errorLabel?.stringValue=L10n.text(startup.note);return};UpdateManager.shared.configure(channel:channel?.indexOfSelectedItem==1 ? "beta":"stable",automatic:updatesToggle?.state == .on);guard UpdateManager.shared.lastSaveSucceeded else{errorLabel?.stringValue=UpdateManager.shared.status;return};UserDefaults.standard.set(UpdateManager.installed,forKey:Self.versionKey);UserDefaults.standard.set(true,forKey:"hasLaunched");UserDefaults.standard.set(true,forKey:"startupPrompted");completed=true;window.close()}
 func windowWillClose(_ notification:Notification){timer?.invalidate();timer=nil;player?.stop();player=nil;Self.active=false;onFinish();if completed{Task{await UpdateManager.shared.check()}}}
}
