import AppKit
import AVFoundation

final class WelcomeName:NSTextField {
 private let gradient=CAGradientLayer()
 var reveal:CGFloat=0{didSet{wantsLayer=true;alphaValue=reveal<=0 ? 0:1;guard reveal<1 else{layer?.mask=nil;return};let edge=reveal*1.4-0.2;CATransaction.begin();CATransaction.setDisableActions(true);gradient.frame=bounds;gradient.startPoint=CGPoint(x:0,y:0.5);gradient.endPoint=CGPoint(x:1,y:0.5);gradient.colors=[NSColor.white.cgColor,NSColor.white.cgColor,NSColor.clear.cgColor,NSColor.clear.cgColor];gradient.locations=[0,NSNumber(value:Double(max(0,min(1,edge-0.2)))),NSNumber(value:Double(max(0,min(1,edge+0.2)))),1];layer?.mask=gradient;CATransaction.commit()}}
}

final class WelcomeController:NSObject,NSWindowDelegate {
 static var active=false
 static let versionKey="welcomeCompletedVersion",splashKey="showSplashOnStartup",newsKey="seenNewsVersion"
 static func needs(_ version:String,defaults:UserDefaults = .standard)->Bool{defaults.string(forKey:versionKey) != version}
 static func needsSetup(defaults:UserDefaults = .standard)->Bool{(defaults.string(forKey:versionKey) ?? "").isEmpty && !defaults.bool(forKey:"startupPrompted") && !defaults.bool(forKey:"hasLaunched")}
 static func needsNews(_ version:String,defaults:UserDefaults = .standard)->Bool{defaults.string(forKey:newsKey) != version}
 static var showOnStartup:Bool{get{UserDefaults.standard.bool(forKey:splashKey)}set{UserDefaults.standard.set(newValue,forKey:splashKey)}}
 let window:NSWindow
 private let root=NSView(),logo=NSImageView(),name=WelcomeName(labelWithString:"WirePulse"),form=NSStackView()
 private let audio:SystemAudio,startup:StartupManager,onFinish:()->Void,initialSetup:Bool,showNews:Bool,audioEnabled:Bool
 private let music=SetupMusic()
 private(set) var phase="intro"
 private(set) var newsAcknowledged=false
 var finished:Bool{completed}
 private var expanded=false,fadeOnly=false,musicButton:NSButton?
 private let acknowledgeNews:()->Void
 private var timer:Timer?,player:AVAudioPlayer?,permissionStatus:NSTextField?,setup=false,completed=false,transitionStart:Double?,introStart=0.0,introDuration=6.136
 private let introNameWidth=("WirePulse" as NSString).size(withAttributes:[.font:NSFont.systemFont(ofSize:30,weight:.bold)]).width
 private var center=NSPoint.zero
 private var startToggle:NSButton?,updatesToggle:NSButton?,splashToggle:NSButton?,channel:NSPopUpButton?,errorLabel:NSTextField?
 init(audio:SystemAudio,startup:StartupManager,initialSetup:Bool=true,playSound:Bool=true,showNews:Bool=false,acknowledgeNews:@escaping()->Void={UserDefaults.standard.set(UpdateManager.installed,forKey:WelcomeController.newsKey)},onFinish:@escaping()->Void){
  self.audio=audio;self.startup=startup;self.initialSetup=initialSetup;self.onFinish=onFinish;self.showNews=showNews;self.audioEnabled=playSound;self.acknowledgeNews=acknowledgeNews
  window=NSWindow(contentRect:NSRect(x:0,y:0,width:320,height:280),styleMask:[.titled,.closable,.fullSizeContentView],backing:.buffered,defer:false)
  super.init();Self.active=true;window.appearance=NSAppearance(named:.darkAqua);window.title="WirePulse";window.titleVisibility = .hidden;window.titlebarAppearsTransparent=true;window.isOpaque=false;window.backgroundColor = .clear;window.isMovableByWindowBackground=true;window.isReleasedWhenClosed=false;window.delegate=self
  window.standardWindowButton(.closeButton)?.isHidden=true;window.standardWindowButton(.miniaturizeButton)?.isHidden=true;window.standardWindowButton(.zoomButton)?.isHidden=true
  // Resolve the native class at runtime so older build SDKs and macOS 14/15 remain supported.
  let backdrop:NSView
  if #available(macOS 26.0,*),let type=NSClassFromString("NSGlassEffectView") as? NSView.Type{let glass=type.init(frame:NSRect(x:0,y:0,width:320,height:280));glass.setValue(root,forKey:"contentView");glass.setValue(28,forKey:"cornerRadius");glass.setValue(1,forKey:"style");glass.setValue(NSColor.black.withAlphaComponent(0.12),forKey:"tintColor");backdrop=glass}
  else{let blur=NSVisualEffectView(frame:NSRect(x:0,y:0,width:320,height:280));blur.material = .hudWindow;blur.blendingMode = .behindWindow;blur.state = .active;blur.wantsLayer=true;blur.layer?.cornerRadius=28;blur.layer?.masksToBounds=true;blur.addSubview(root);backdrop=blur}
  window.contentView=backdrop;root.frame=backdrop.bounds;root.autoresizingMask=[.width,.height];root.wantsLayer=true
  logo.image=NSImage(named:NSImage.applicationIconName);logo.imageScaling = .scaleProportionallyUpOrDown;root.addSubview(logo);name.isBezeled=false;name.drawsBackground=false;name.isEditable=false;name.textColor = .white;root.addSubview(name)
  form.orientation = .vertical;form.alignment = .leading;form.spacing=10;form.frame=NSRect(x:36,y:28,width:548,height:500);root.addSubview(form)
  window.center();center=NSPoint(x:window.frame.midX,y:window.frame.midY);NSApp.activate(ignoringOtherApps:true);window.makeKeyAndOrderFront(nil);splash(playSound:playSound)
 }
 private func label(_ text:String,_ size:CGFloat=14)->NSTextField{let field=NSTextField(wrappingLabelWithString:L10n.text(text));field.font = .systemFont(ofSize:size);field.textColor = .white;field.preferredMaxLayoutWidth=548;return field}
 private func button(_ title:String,_ action:Selector)->NSButton{let b=NSButton(title:L10n.text(title),target:self,action:action);b.bezelStyle = .rounded;return b}
 private func clearForm(){for view in form.arrangedSubviews{form.removeArrangedSubview(view);view.removeFromSuperview()}}
 @objc private func requestAudio(){audio.start()}
 @objc private func openPermissions(){if let url=URL(string:"x-apple.systempreferences:com.apple.preference.security?Privacy_AudioCapture"){NSWorkspace.shared.open(url)}}
 private func refreshPermission(){permissionStatus?.stringValue=L10n.text(audio.running ? "Audio del sistema preparado.":audio.status)}
 func splash(playSound:Bool=true){timer?.invalidate();player?.stop();clearForm();form.isHidden=true;setup=false;phase="intro";transitionStart=nil;name.reveal=0;layout(0)
  if let url=Bundle.main.url(forResource:"Welcome",withExtension:"wav"){do{let sound=try AVAudioPlayer(contentsOf:url);introDuration=sound.duration;if playSound{player=sound;sound.prepareToPlay();sound.play()}}catch{NSLog("Welcome audio: %@",error.localizedDescription)}}
  introStart=ProcessInfo.processInfo.systemUptime;timer=Timer.scheduledTimer(withTimeInterval:1.0/60,repeats:true){[weak self] _ in self?.animate()}
 }
 private func animate(){let now=ProcessInfo.processInfo.systemUptime
  if let start=transitionStart{let progress=max(0,min(1,(now-start)/0.8)),eased=1-pow(1-progress,3);if !fadeOnly{layout(eased)};form.alphaValue=max(0,min(1,(eased-0.25)/0.75));if progress>=1{transitionStart=nil;timer?.invalidate();if phase=="setup"{timer=Timer.scheduledTimer(withTimeInterval:0.5,repeats:true){[weak self] _ in self?.refreshPermission()}}};return}
  guard phase=="intro" else{return}
  name.reveal=CGFloat(max(0,min(1,(now-introStart)/introDuration)))
  if now-introStart>=introDuration+0.15 && (player?.isPlaying != true || now-introStart>=introDuration+4){showSetup()}
 }
 private func layout(_ fraction:Double){let t=CGFloat(fraction),w=320+300*t,h=280+360*t
  window.setFrame(NSRect(x:center.x-w/2,y:center.y-h/2,width:w,height:h),display:true)
  let size=112-70*t;logo.frame=NSRect(x:104-76*t,y:h-(42-18*t)-size,width:size,height:size)
  name.font = .systemFont(ofSize:30-8*t,weight:.bold);name.alignment = .left;let initialX=(320-introNameWidth)/2;name.frame=NSRect(x:initialX+(84-initialX)*t,y:h-(174-143*t)-42,width:190,height:42)
 }
 func showSetup(){guard phase=="intro" else{return};if !initialSetup{if showNews{showNewsPage()}else{window.close()};return};setup=true;phase="setup";clearForm();form.spacing=10
  form.addArrangedSubview(label("Vamos a preparar tu escritorio",23))
  form.addArrangedSubview(label("Activa el permiso de audio para que el fondo reaccione a tu música. Puedes continuar sin activarlo."))
  let permissions=NSStackView(views:[button("Activar audio",#selector(requestAudio)),button("Abrir permisos del sistema",#selector(openPermissions))]);permissions.orientation = .horizontal;permissions.spacing=10;form.addArrangedSubview(permissions);let status=label(audio.status,12);permissionStatus=status;form.addArrangedSubview(status)
  startup.refresh();let start=NSButton(checkboxWithTitle:L10n.text("Abrir WirePulse al iniciar sesión"),target:nil,action:nil);start.state=startup.enabled ? .on:.off;startToggle=start;form.addArrangedSubview(start)
  let updates=NSButton(checkboxWithTitle:L10n.text("Comprobar actualizaciones automáticamente"),target:nil,action:nil);updates.state=UpdateManager.shared.preferences.automatic ? .on:.off;updatesToggle=updates;form.addArrangedSubview(updates)
  let splash=NSButton(checkboxWithTitle:L10n.text("Mostrar la splash en cada inicio"),target:nil,action:nil);splash.state=Self.showOnStartup ? .on:.off;splashToggle=splash;form.addArrangedSubview(splash)
  form.addArrangedSubview(label("Canal de actualizaciones"));let picker=NSPopUpButton();picker.addItems(withTitles:[L10n.text("Estable"),L10n.text("Beta (prereleases)")]);picker.selectItem(at:UpdateManager.shared.preferences.channel=="beta" ? 1:0);channel=picker;form.addArrangedSubview(picker)
  form.addArrangedSubview(label("Puedes cambiar estas opciones más adelante en Ajustes."));let error=label("");error.textColor = .systemOrange;errorLabel=error;form.addArrangedSubview(error);let finish=button(showNews ? "Continuar":"Empezar",#selector(save));finish.keyEquivalent="\r";form.addArrangedSubview(footer(finish));expand()
 }
 @objc private func save(){let desired=startToggle?.state == .on;if desired != startup.enabled{startup.setEnabled(desired)};guard startup.enabled==desired else{errorLabel?.stringValue=L10n.text(startup.note);return};UpdateManager.shared.configure(channel:channel?.indexOfSelectedItem==1 ? "beta":"stable",automatic:updatesToggle?.state == .on);guard UpdateManager.shared.lastSaveSucceeded else{errorLabel?.stringValue=UpdateManager.shared.status;return};Self.showOnStartup=splashToggle?.state == .on;UserDefaults.standard.set(UpdateManager.installed,forKey:Self.versionKey);UserDefaults.standard.set(true,forKey:"hasLaunched");UserDefaults.standard.set(true,forKey:"startupPrompted");completed=true;if showNews{showNewsPage()}else{window.close()}}
 @objc private func toggleMusic(){music.muted.toggle();musicButton?.title=L10n.text(music.muted ? "Activar música":"Silenciar música")}
 private func footer(_ finish:NSButton)->NSStackView{let mute=button(music.muted ? "Activar música":"Silenciar música",#selector(toggleMusic));musicButton=mute;let row=NSStackView(views:[mute,finish]);row.orientation = .horizontal;row.spacing=12;return row}
 private func expand(){player?.stop();player=nil;if audioEnabled{music.start()};form.isHidden=false;form.alphaValue=0;name.reveal=1;window.standardWindowButton(.closeButton)?.isHidden=false;fadeOnly=expanded;if !expanded{center=NSPoint(x:window.frame.midX,y:window.frame.midY);expanded=true};transitionStart=ProcessInfo.processInfo.systemUptime;timer?.invalidate();timer=Timer.scheduledTimer(withTimeInterval:1.0/60,repeats:true){[weak self] _ in self?.animate()}}
 func showNewsPage(){guard phase != "news" else{return};phase="news";clearForm();permissionStatus=nil;form.spacing=18
  let title=label("Novedades de\nWirePulse",32);title.font = .systemFont(ofSize:32,weight:.bold);title.alignment = .center;title.widthAnchor.constraint(equalToConstant:548).isActive=true;form.addArrangedSubview(title)
  let version=label(UpdateManager.installed,13);version.textColor = .systemPink;version.alignment = .center;version.widthAnchor.constraint(equalToConstant:548).isActive=true;form.addArrangedSubview(version)
  let rows=NSStackView();rows.orientation = .vertical;rows.alignment = .leading;rows.spacing=22
  for feature in WhatsNewContent.current.features{let symbol=feature.icon=="music" ? "music.note":feature.icon=="update" ? "arrow.triangle.2.circlepath":"sparkles";let icon=NSImageView(image:NSImage(systemSymbolName:symbol,accessibilityDescription:L10n.text(feature.title)) ?? NSImage());icon.contentTintColor = .systemPink;icon.imageScaling = .scaleProportionallyUpOrDown;icon.widthAnchor.constraint(equalToConstant:32).isActive=true;icon.heightAnchor.constraint(equalToConstant:36).isActive=true
   let copy=NSStackView();copy.orientation = .vertical;copy.alignment = .leading;copy.spacing=5;let heading=label(feature.title,18);heading.font = .systemFont(ofSize:18,weight:.semibold);let detail=label(feature.description,14);detail.textColor = .secondaryLabelColor;for text in [heading,detail]{text.preferredMaxLayoutWidth=498;text.widthAnchor.constraint(equalToConstant:498).isActive=true;copy.addArrangedSubview(text)}
   let row=NSStackView(views:[icon,copy]);row.orientation = .horizontal;row.alignment = .top;row.spacing=18;row.widthAnchor.constraint(equalToConstant:548).isActive=true;rows.addArrangedSubview(row)
  }
  let scroll=NSScrollView(frame:NSRect(x:0,y:0,width:548,height:320));scroll.drawsBackground=false;scroll.hasVerticalScroller=true;scroll.autohidesScrollers=true;scroll.borderType = .noBorder;rows.layoutSubtreeIfNeeded();rows.frame=NSRect(x:0,y:0,width:548,height:max(320,rows.fittingSize.height));scroll.documentView=rows;scroll.widthAnchor.constraint(equalToConstant:548).isActive=true;scroll.heightAnchor.constraint(equalToConstant:320).isActive=true;scroll.contentView.scroll(to:NSPoint(x:0,y:rows.isFlipped ? 0:max(0,rows.frame.height-320)));form.addArrangedSubview(scroll);let finish=button("Continuar",#selector(finishNews));finish.keyEquivalent="\r";form.addArrangedSubview(footer(finish));expand()
 }
 @objc private func finishNews(){acknowledgeNews();newsAcknowledged=true;completed=true;window.close()}
 func windowWillClose(_ notification:Notification){timer?.invalidate();timer=nil;player?.stop();player=nil;music.stop();Self.active=false;onFinish()}
}
