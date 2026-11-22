import AppKit
import SwiftUI
import MetalKit
import Combine

final class WallpaperSurface {
 let config:Configuration;let window:NSWindow;let view:MTKView;let renderer:WallpaperRenderer;let background:BackgroundMedia;let screen:NSScreen
 var suspensionReason="";private var suspended=false
 private var imagePath=""
 private var sceneRoot:SceneRoot?
 private let backgroundDim=CALayer()
 private let audio:SystemAudio,media:NowPlaying
 private var legacy=[NSView]()
 init(screen:NSScreen,config:Configuration,audio:SystemAudio,media:NowPlaying)throws{
  self.config=config;self.audio=audio;self.media=media;self.screen=screen
  guard let device=MTLCreateSystemDefaultDevice() else{throw NSError(domain:"WirePulse",code:3,userInfo:[NSLocalizedDescriptionKey:"Este Mac no dispone de Metal"])}
  renderer=try WallpaperRenderer(config:config,analyzer:audio.analyzer,device:device,media:media)
  window=NSWindow(contentRect:screen.frame,styleMask:.borderless,backing:.buffered,defer:false,screen:screen)
  window.title=L10n.text("WirePulse · fondo animado");window.level=NSWindow.Level(rawValue:Int(CGWindowLevelForKey(.desktopWindow))+1);window.collectionBehavior=[.canJoinAllSpaces,.stationary,.ignoresCycle];window.ignoresMouseEvents=true;window.hasShadow=false;window.isOpaque=true;window.isReleasedWhenClosed=false;window.hidesOnDeactivate=false
  let root=NSView(frame:NSRect(origin:.zero,size:screen.frame.size));root.wantsLayer=true;window.contentView=root
  background=BackgroundMedia(frame:root.bounds);background.wantsLayer=true;background.layer?.contentsGravity = .resizeAspectFill;background.layer?.masksToBounds=true;background.autoresizingMask=[.width,.height];root.addSubview(background);backgroundDim.backgroundColor=NSColor.black.cgColor;background.layer?.addSublayer(backgroundDim)
  view=MTKView(frame:root.bounds,device:device);view.colorPixelFormat = .bgra8Unorm;view.autoResizeDrawable=false;view.framebufferOnly=true;view.layer?.isOpaque=false;view.autoresizingMask=[.width,.height];view.delegate=renderer;view.preferredFramesPerSecond=Int(config.n("fps"));root.addSubview(view)
  let overlay=NSHostingView(rootView:MusicCard(config:config,media:media,audio:audio));overlay.frame=root.bounds;overlay.autoresizingMask=[.width,.height];root.addSubview(overlay)
  let clock=NSHostingView(rootView:ClockOverlay(config:config));clock.frame=root.bounds;clock.autoresizingMask=[.width,.height];root.addSubview(clock)
  legacy=[view,overlay,clock]
  update(config:config);window.orderFrontRegardless()
 }
 func update(config:Configuration){
  backgroundDim.frame=background.bounds;backgroundDim.opacity=config.scene != nil && !config.backgroundPath.isEmpty ? Float(config.n("dim")/100):0

  for child in legacy{child.isHidden=config.scene != nil};view.isPaused=config.scene != nil
  if config.scene != nil{if sceneRoot==nil,let root=window.contentView{let scene=SceneRoot(frame:root.bounds,config:config,audio:audio,media:media);scene.autoresizingMask=[.width,.height];root.addSubview(scene);sceneRoot=scene};sceneRoot?.refresh()}else{sceneRoot?.removeFromSuperview();sceneRoot=nil}
background.layer?.backgroundColor=config.color("backgroundcolor").cgColor
  if imagePath != config.backgroundPath{imagePath=config.backgroundPath;background.load(imagePath)}
  suspend(suspended)
 }
 func suspend(_ paused:Bool){suspended=paused;view.isPaused=paused || config.paused || config.scene != nil;sceneRoot?.pause(paused);background.suspend(paused || config.paused)}
 var frameCount:UInt64{sceneRoot?.frameCount ?? renderer.frameCount}
 var fps:Double{suspended ? 0:(sceneRoot?.fps ?? renderer.fps)}
 func close(){suspend(true);window.orderOut(nil);window.close()}
}
final class AppDelegate:NSObject,NSApplicationDelegate{
 let config=Configuration(),audio=SystemAudio(),media=NowPlaying(),startup=StartupManager()
 private var editorWindow:SceneEditorWindow?
 private var surfaces=[WallpaperSurface](),item:NSStatusItem!,settingsWindow:NSWindow?,timer:Timer?,subscriptions=Set<AnyCancellable>(),observers=[NSObjectProtocol](),resumeAudio=false,displayProfiles=[String:Configuration](),lastIndependent=false,sessionSuspended=false
 private var coverageTimer:Timer?
 func applicationDidFinishLaunching(_ notification:Notification){
  NSApp.setActivationPolicy(.accessory)
  item=NSStatusBar.system.statusItem(withLength:NSStatusItem.squareLength);item.button?.image=NSImage(systemSymbolName:"waveform.circle.fill",accessibilityDescription:"WirePulse")
  let menu=NSMenu();menu.addItem(withTitle:L10n.text("WirePulse · Ajustes…"),action:#selector(showSettings),keyEquivalent:",");menu.addItem(withTitle:L10n.text("Editar escritorio…"),action:#selector(editDesktop),keyEquivalent:"e");menu.addItem(withTitle:L10n.text("Pausar / reanudar animación"),action:#selector(toggleAnimation),keyEquivalent:"");menu.addItem(withTitle:L10n.text("Reiniciar audio del sistema"),action:#selector(restartAudio),keyEquivalent:"");menu.addItem(.separator());menu.addItem(withTitle:L10n.text("Salir de WirePulse"),action:#selector(quit),keyEquivalent:"q");for entry in menu.items{entry.target=self};item.menu=menu
  let menuBar=NSMenu(),appMenuItem=NSMenuItem();menuBar.addItem(appMenuItem);appMenuItem.submenu=menu.copy() as? NSMenu;NSApp.mainMenu=menuBar
  lastIndependent=config.independentDisplays;rebuild()
  config.objectWillChange.sink{[weak self] _ in DispatchQueue.main.async{guard let self=self else{return};if self.lastIndependent != self.config.independentDisplays{self.lastIndependent=self.config.independentDisplays;if self.lastIndependent{for p in self.displayProfiles.values{if !p.hasSaved{p.copyLayout(from:self.config)}}};self.rebuild()}else{for surface in self.surfaces{surface.update(config:surface.config)}}}}.store(in:&subscriptions)
  observers.append(NotificationCenter.default.addObserver(forName:NSApplication.didChangeScreenParametersNotification,object:nil,queue:.main){[weak self] _ in self?.rebuild()})
  for name in [NSWorkspace.screensDidSleepNotification,NSWorkspace.sessionDidResignActiveNotification]{observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName:name,object:nil,queue:.main){[weak self] _ in guard let self=self else{return};self.resumeAudio=self.resumeAudio||self.audio.requested;self.audio.stop();self.sessionSuspended=true;self.updateSuspensions()})}
  for name in [NSWorkspace.screensDidWakeNotification,NSWorkspace.sessionDidBecomeActiveNotification]{observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName:name,object:nil,queue:.main){[weak self] _ in guard let self=self else{return};self.sessionSuspended=false;self.updateSuspensions();if self.resumeAudio{self.resumeAudio=false;self.audio.start()}})}
  coverageTimer=Timer.scheduledTimer(withTimeInterval:1,repeats:true){[weak self] _ in self?.updateSuspensions()}
  timer=Timer.scheduledTimer(withTimeInterval:2,repeats:true){[weak self] _ in guard let self=self else{return};self.media.poll(source:self.config.mediaSource,ciderURL:self.config.ciderURL,ciderToken:self.config.ciderToken);self.writeStatus()}
  if !UserDefaults.standard.bool(forKey:"hasLaunched"){showSettings();UserDefaults.standard.set(true,forKey:"hasLaunched")}
  DispatchQueue.main.asyncAfter(deadline:.now()+0.5){[weak self] in self?.startup.promptIfNeeded()}
  DispatchQueue.main.asyncAfter(deadline:.now()+0.8){[weak self] in self?.audio.start()}
 }
 private func rebuild(){if let window=settingsWindow{window.contentView=nil};for s in surfaces{s.close()};surfaces=[];do{for screen in NSScreen.screens{let profile=profileFor(screen);surfaces.append(try WallpaperSurface(screen:screen,config:config.independentDisplays ? profile:config,audio:audio,media:media))}}catch{NSAlert(error:L10n.error(error)).runModal()};if let window=settingsWindow{window.contentView=NSHostingView(rootView:DisplaySettingsView(root:config,audio:audio,media:media,profiles:displayProfiles,startup:startup,edit:{[weak self] target in self?.openEditor(target)}))}}
 private func profileFor(_ screen:NSScreen)->Configuration{let key=displayKey(screen);if let p=displayProfiles[key]{return p};let p=Configuration(profile:key,template:config);displayProfiles[key]=p;p.objectWillChange.sink{[weak self] _ in DispatchQueue.main.async{guard let self=self else{return};for surface in self.surfaces{surface.update(config:surface.config)}}}.store(in:&subscriptions);return p}
 func applicationShouldHandleReopen(_ sender:NSApplication,hasVisibleWindows flag:Bool)->Bool{showSettings();return true}
 @objc func showSettings(){
  if settingsWindow==nil{let window=NSWindow(contentRect:NSRect(x:0,y:0,width:820,height:680),styleMask:[.titled,.closable,.miniaturizable,.resizable],backing:.buffered,defer:false);window.title=L10n.text("WirePulse · Yamato · Ajustes");window.contentView=NSHostingView(rootView:DisplaySettingsView(root:config,audio:audio,media:media,profiles:displayProfiles,startup:startup,edit:{[weak self] target in self?.openEditor(target)}));window.center();window.isReleasedWhenClosed=false;settingsWindow=window}
  NSApp.activate(ignoringOtherApps:true);settingsWindow?.makeKeyAndOrderFront(nil)
 }
 @objc func editDesktop(){openEditor(config.independentDisplays ? profileFor(NSScreen.main ?? NSScreen.screens[0]):config)}
 func openEditor(_ target:Configuration){if let editor=editorWindow,editor.isVisible{editor.makeKeyAndOrderFront(nil);return};let screen=NSScreen.screens.first{config.independentDisplays && displayProfiles[displayKey($0)] === target} ?? NSScreen.main ?? NSScreen.screens[0];let editor=SceneEditorWindow(screen:screen,config:target,audio:audio,media:media);editorWindow=editor;updateSuspensions();editor.onFinish={[weak self] in self?.editorWindow=nil;self?.updateSuspensions()};NSApp.activate(ignoringOtherApps:true);editor.makeKeyAndOrderFront(nil)}
 private func updateSuspensions(){let windows=(CGWindowListCopyWindowInfo([.optionOnScreenOnly,.excludeDesktopElements],kCGNullWindowID) as? [[String:Any]]) ?? [];for surface in surfaces{let editing=editorWindow?.screen.map{displayKey($0)==displayKey(surface.screen)} ?? false;surface.suspensionReason=sessionSuspended ? "sesión suspendida":editing ? "editor de escritorio":DisplayPolicy.reason(screen:surface.screen,config:surface.config,windows:windows);surface.suspend(!surface.suspensionReason.isEmpty)}}
 @objc func toggleAnimation(){config.paused.toggle();for profile in displayProfiles.values{profile.paused=config.paused}}
 @objc func restartAudio(){audio.start()}
 @objc func quit(){NSApp.terminate(nil)}
 func applicationWillTerminate(_ notification:Notification){audio.stop();for s in surfaces{s.close()};timer?.invalidate();coverageTimer?.invalidate();for observer in observers{NotificationCenter.default.removeObserver(observer);NSWorkspace.shared.notificationCenter.removeObserver(observer)}}
 private func writeStatus(){let snapshot=audio.analyzer.snapshot();let folder=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("WirePulse",isDirectory:true)
  let status:[String:Any]=["pid":ProcessInfo.processInfo.processIdentifier,"executablePath":currentExecutableURL()?.path ?? "unavailable","version":Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "development","nativeRenderer":"Metal","audioBackend":"Core Audio process tap","audioRunning":audio.running,"mediaSource":config.mediaSource,"detectedProvider":media.provider,"hasArtwork":media.artwork != nil,"artworkStatus":media.artworkState,"backgroundColor":config.colors["backgroundcolor"] ?? "","spectrumY":config.n("spectrumY"),"spectrumHeight":config.n("spectrumHeight"),"audioStatus":audio.status,"beat":snapshot.beat,"bpm":snapshot.bpm,"beatConfidence":snapshot.beatConfidence,"fftFrames":snapshot.frames,"sampleRate":snapshot.sampleRate,"fftSize":snapshot.fftSize,"binWidthHz":snapshot.binWidthHz,"maximumHz":snapshot.maximumHz,"peakFrequencyHz":snapshot.peakFrequencyHz,"lastAudioAge":snapshot.age,"peakBand":snapshot.peak,"screenCount":surfaces.count,"layerDrag":LayerTable.dragStatus,"renderedFrames":surfaces.map{$0.frameCount},"displays":surfaces.map{["name":$0.screen.localizedName,"refreshHz":$0.screen.maximumFramesPerSecond,"renderedFps":$0.fps,"suspensionReason":$0.suspensionReason] as [String:Any]},"timestamp":Date().timeIntervalSince1970]
  do{try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true);try JSONSerialization.data(withJSONObject:status,options:[.prettyPrinted,.sortedKeys]).write(to:folder.appendingPathComponent("status.json"),options:.atomic)}catch{}
 }
}
