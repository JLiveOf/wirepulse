import AppKit
import SwiftUI
import MetalKit
import Combine

final class WallpaperSurface {
 let config:Configuration;let window:NSWindow;let view:MTKView;let renderer:WallpaperRenderer;let background:NSView
 private var imagePath=""
 init(screen:NSScreen,config:Configuration,audio:SystemAudio,media:NowPlaying)throws{
  self.config=config
  guard let device=MTLCreateSystemDefaultDevice() else{throw NSError(domain:"WirePulse",code:3,userInfo:[NSLocalizedDescriptionKey:"Este Mac no dispone de Metal"])}
  renderer=try WallpaperRenderer(config:config,analyzer:audio.analyzer,device:device)
  window=NSWindow(contentRect:screen.frame,styleMask:.borderless,backing:.buffered,defer:false,screen:screen)
  window.title="WirePulse · fondo animado";window.level=NSWindow.Level(rawValue:Int(CGWindowLevelForKey(.desktopWindow))+1);window.collectionBehavior=[.canJoinAllSpaces,.stationary,.ignoresCycle];window.ignoresMouseEvents=true;window.hasShadow=false;window.isOpaque=true;window.isReleasedWhenClosed=false;window.hidesOnDeactivate=false
  let root=NSView(frame:NSRect(origin:.zero,size:screen.frame.size));root.wantsLayer=true;window.contentView=root
  background=NSView(frame:root.bounds);background.wantsLayer=true;background.layer?.contentsGravity = .resizeAspectFill;background.layer?.masksToBounds=true;background.autoresizingMask=[.width,.height];root.addSubview(background)
  view=MTKView(frame:root.bounds,device:device);view.colorPixelFormat = .bgra8Unorm;view.autoResizeDrawable=false;view.framebufferOnly=true;view.layer?.isOpaque=false;view.autoresizingMask=[.width,.height];view.delegate=renderer;view.preferredFramesPerSecond=Int(config.n("fps"));root.addSubview(view)
  let overlay=NSHostingView(rootView:MusicCard(config:config,media:media,audio:audio));overlay.frame=root.bounds;overlay.autoresizingMask=[.width,.height];root.addSubview(overlay)
  let clock=NSHostingView(rootView:ClockOverlay(config:config));clock.frame=root.bounds;clock.autoresizingMask=[.width,.height];root.addSubview(clock)
  update(config:config);window.orderFrontRegardless()
 }
 func update(config:Configuration){background.layer?.backgroundColor=config.color("backgroundcolor").cgColor
  if imagePath != config.backgroundPath{imagePath=config.backgroundPath;background.layer?.contents=NSImage(contentsOfFile:imagePath)?.cgImage(forProposedRect:nil,context:nil,hints:nil)}
 }
 func close(){view.isPaused=true;window.orderOut(nil);window.close()}
}
final class AppDelegate:NSObject,NSApplicationDelegate{
 let config=Configuration(),audio=SystemAudio(),media=NowPlaying(),startup=StartupManager()
 private var surfaces=[WallpaperSurface](),item:NSStatusItem!,settingsWindow:NSWindow?,timer:Timer?,subscriptions=Set<AnyCancellable>(),observers=[NSObjectProtocol](),resumeAudio=false,displayProfiles=[String:Configuration](),lastIndependent=false
 func applicationDidFinishLaunching(_ notification:Notification){
  NSApp.setActivationPolicy(.accessory)
  item=NSStatusBar.system.statusItem(withLength:NSStatusItem.squareLength);item.button?.image=NSImage(systemSymbolName:"waveform.circle.fill",accessibilityDescription:"WirePulse")
  let menu=NSMenu();menu.addItem(withTitle:"WirePulse · Ajustes…",action:#selector(showSettings),keyEquivalent:",");menu.addItem(withTitle:"Pausar / reanudar animación",action:#selector(toggleAnimation),keyEquivalent:"");menu.addItem(withTitle:"Reiniciar audio del sistema",action:#selector(restartAudio),keyEquivalent:"");menu.addItem(.separator());menu.addItem(withTitle:"Salir de WirePulse",action:#selector(quit),keyEquivalent:"q");for entry in menu.items{entry.target=self};item.menu=menu
  lastIndependent=config.independentDisplays;rebuild()
  config.objectWillChange.sink{[weak self] _ in DispatchQueue.main.async{guard let self=self else{return};if self.lastIndependent != self.config.independentDisplays{self.lastIndependent=self.config.independentDisplays;if self.lastIndependent{for p in self.displayProfiles.values{if !p.hasSaved{p.copyLayout(from:self.config)}}};self.rebuild()}else{for surface in self.surfaces{surface.update(config:surface.config)}}}}.store(in:&subscriptions)
  observers.append(NotificationCenter.default.addObserver(forName:NSApplication.didChangeScreenParametersNotification,object:nil,queue:.main){[weak self] _ in self?.rebuild()})
  for name in [NSWorkspace.screensDidSleepNotification,NSWorkspace.sessionDidResignActiveNotification]{observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName:name,object:nil,queue:.main){[weak self] _ in guard let self=self else{return};self.resumeAudio=self.resumeAudio||self.audio.running;self.audio.stop();for s in self.surfaces{s.view.isPaused=true}})}
  for name in [NSWorkspace.screensDidWakeNotification,NSWorkspace.sessionDidBecomeActiveNotification]{observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName:name,object:nil,queue:.main){[weak self] _ in guard let self=self else{return};for s in self.surfaces{s.view.isPaused=false};if self.resumeAudio{self.resumeAudio=false;self.audio.start()}})}
  timer=Timer.scheduledTimer(withTimeInterval:2,repeats:true){[weak self] _ in guard let self=self else{return};self.media.poll(source:self.config.mediaSource,ciderURL:self.config.ciderURL,ciderToken:self.config.ciderToken);self.writeStatus()}
  if !UserDefaults.standard.bool(forKey:"hasLaunched"){showSettings();UserDefaults.standard.set(true,forKey:"hasLaunched")}
  DispatchQueue.main.asyncAfter(deadline:.now()+0.5){[weak self] in self?.startup.promptIfNeeded()}
  DispatchQueue.main.asyncAfter(deadline:.now()+0.8){[weak self] in self?.audio.start()}
 }
 private func rebuild(){if let window=settingsWindow{window.contentView=nil};for s in surfaces{s.close()};surfaces=[];do{for screen in NSScreen.screens{let profile=profileFor(screen);surfaces.append(try WallpaperSurface(screen:screen,config:config.independentDisplays ? profile:config,audio:audio,media:media))}}catch{NSAlert(error:error).runModal()};if let window=settingsWindow{window.contentView=NSHostingView(rootView:DisplaySettingsView(root:config,audio:audio,media:media,profiles:displayProfiles,startup:startup))}}
 private func profileFor(_ screen:NSScreen)->Configuration{let key=displayKey(screen);if let p=displayProfiles[key]{return p};let p=Configuration(profile:key,template:config);displayProfiles[key]=p;p.objectWillChange.sink{[weak self] _ in DispatchQueue.main.async{guard let self=self else{return};for surface in self.surfaces{surface.update(config:surface.config)}}}.store(in:&subscriptions);return p}
 @objc func showSettings(){
  if settingsWindow==nil{let window=NSWindow(contentRect:NSRect(x:0,y:0,width:820,height:680),styleMask:[.titled,.closable,.miniaturizable,.resizable],backing:.buffered,defer:false);window.title="WirePulse · Ajustes";window.contentView=NSHostingView(rootView:DisplaySettingsView(root:config,audio:audio,media:media,profiles:displayProfiles,startup:startup));window.center();window.isReleasedWhenClosed=false;settingsWindow=window}
  NSApp.activate(ignoringOtherApps:true);settingsWindow?.makeKeyAndOrderFront(nil)
 }
 @objc func toggleAnimation(){config.paused.toggle();for profile in displayProfiles.values{profile.paused=config.paused}}
 @objc func restartAudio(){audio.start()}
 @objc func quit(){NSApp.terminate(nil)}
 func applicationWillTerminate(_ notification:Notification){audio.stop();for s in surfaces{s.close()};timer?.invalidate();for observer in observers{NotificationCenter.default.removeObserver(observer);NSWorkspace.shared.notificationCenter.removeObserver(observer)}}
 private func writeStatus(){let snapshot=audio.analyzer.snapshot();let folder=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("WirePulse",isDirectory:true)
  let status:[String:Any]=["pid":ProcessInfo.processInfo.processIdentifier,"executablePath":currentExecutableURL()?.path ?? "unavailable","version":Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "development","nativeRenderer":"Metal","audioBackend":"Core Audio process tap","audioRunning":audio.running,"mediaSource":config.mediaSource,"detectedProvider":media.provider,"hasArtwork":media.artwork != nil,"artworkStatus":media.artworkState,"backgroundColor":config.colors["backgroundcolor"] ?? "","spectrumY":config.n("spectrumY"),"spectrumHeight":config.n("spectrumHeight"),"audioStatus":audio.status,"beat":snapshot.beat,"bpm":snapshot.bpm,"beatConfidence":snapshot.beatConfidence,"fftFrames":snapshot.frames,"sampleRate":snapshot.sampleRate,"fftSize":snapshot.fftSize,"binWidthHz":snapshot.binWidthHz,"maximumHz":snapshot.maximumHz,"peakFrequencyHz":snapshot.peakFrequencyHz,"lastAudioAge":snapshot.age,"peakBand":snapshot.peak,"screenCount":surfaces.count,"renderedFrames":surfaces.map{$0.renderer.frameCount},"timestamp":Date().timeIntervalSince1970]
  do{try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true);try JSONSerialization.data(withJSONObject:status,options:[.prettyPrinted,.sortedKeys]).write(to:folder.appendingPathComponent("status.json"),options:.atomic)}catch{}
 }
}
