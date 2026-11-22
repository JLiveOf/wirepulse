import AppKit
import Combine
import MetalKit
import SwiftUI
import WebKit

struct SceneItem: Codable, Identifiable, Equatable {
  var id = UUID().uuidString, kind = "orb", name = "Orbe"
  var rotation = 0.0
  var x = 0.35, y = 0.3, width = 0.3, height = 0.4
  var visible = true, locked = false, keepAspect = true, opacity = 1.0
  var asset = "", text = "Tu texto", font = "System", color = "#ffffff"
  var gradients=[String:GradientSpec]()
  var textSize=0.0,textSpeed=1.0,textDistance=12.0
  var textBold=false,textItalic=false,textAlign="center",textAnimation="none"
  var style = [String: SceneValue]()
  mutating func cleanEffects(){textSize=textSize.isFinite ? min(512,max(0,textSize)):0;textSpeed=textSpeed.isFinite ? min(5,max(0.1,textSpeed)):1;textDistance=textDistance.isFinite ? min(200,max(0,textDistance)):12;if !TextMotion.modes.contains(textAnimation){textAnimation="none"};if !["left","center","right"].contains(textAlign){textAlign="center"};gradients=Dictionary(uniqueKeysWithValues:gradients.prefix(32).map{($0.key,$0.value.cleaned())})}
  func rect(_ size: CGSize) -> CGRect {
    CGRect(
      x: x * size.width, y: y * size.height, width: width * size.width, height: height * size.height
    )
  }
  mutating func setRect(_ r: CGRect, _ size: CGSize) {
    x = r.minX / size.width
    y = r.minY / size.height
    width = r.width / size.width
    height = r.height / size.height
  }
}
extension SceneItem {
  enum CodingKeys: String, CodingKey {
    case id, kind, name, x, y, width, height, visible, locked, keepAspect, opacity, asset, text,
      font, color, style, rotation, gradients,textSize,textBold,textItalic,textAlign,textAnimation,textSpeed,textDistance
  }
  init(from decoder: Decoder) throws {
    self.init()
    let c = try decoder.container(keyedBy: CodingKeys.self)
    id = try c.decodeIfPresent(String.self, forKey: .id) ?? id
    kind = try c.decodeIfPresent(String.self, forKey: .kind) ?? kind
    name = try c.decodeIfPresent(String.self, forKey: .name) ?? name
    x = try c.decodeIfPresent(Double.self, forKey: .x) ?? x
    y = try c.decodeIfPresent(Double.self, forKey: .y) ?? y
    width = try c.decodeIfPresent(Double.self, forKey: .width) ?? width
    height = try c.decodeIfPresent(Double.self, forKey: .height) ?? height
    rotation = try c.decodeIfPresent(Double.self, forKey: .rotation) ?? 0
    visible = try c.decodeIfPresent(Bool.self, forKey: .visible) ?? true
    locked = try c.decodeIfPresent(Bool.self, forKey: .locked) ?? false
    keepAspect = try c.decodeIfPresent(Bool.self, forKey: .keepAspect) ?? true
    opacity = try c.decodeIfPresent(Double.self, forKey: .opacity) ?? 1
    asset = try c.decodeIfPresent(String.self, forKey: .asset) ?? ""
    text = try c.decodeIfPresent(String.self, forKey: .text) ?? "Tu texto"
    font = try c.decodeIfPresent(String.self, forKey: .font) ?? "System"
    color = try c.decodeIfPresent(String.self, forKey: .color) ?? "#ffffff"
    gradients=try c.decodeIfPresent([String:GradientSpec].self,forKey:.gradients) ?? [:]
    textSize=try c.decodeIfPresent(Double.self,forKey:.textSize) ?? 0
    textBold=try c.decodeIfPresent(Bool.self,forKey:.textBold) ?? false
    textItalic=try c.decodeIfPresent(Bool.self,forKey:.textItalic) ?? false
    textAlign=try c.decodeIfPresent(String.self,forKey:.textAlign) ?? "center"
    textAnimation=try c.decodeIfPresent(String.self,forKey:.textAnimation) ?? "none"
    textSpeed=try c.decodeIfPresent(Double.self,forKey:.textSpeed) ?? 1
    textDistance=try c.decodeIfPresent(Double.self,forKey:.textDistance) ?? 12
    style = try c.decodeIfPresent([String: SceneValue].self, forKey: .style) ?? [:]
  }
}
enum SceneValue: Codable, Equatable {
  case number(Double)
  case string(String)
  case bool(Bool)
  init(from decoder: Decoder) throws {
    let c = try decoder.singleValueContainer()
    if let b = try? c.decode(Bool.self) {
      self = .bool(b)
    } else if let n = try? c.decode(Double.self) {
      self = .number(n)
    } else {
      self = .string(try c.decode(String.self))
    }
  }
  func encode(to encoder: Encoder) throws {
    var c = encoder.singleValueContainer()
    switch self {
    case .number(let n): try c.encode(n)
    case .string(let s): try c.encode(s)
    case .bool(let b): try c.encode(b)
    }
  }
}
struct SceneDocument: Codable, Equatable {
  var version = 1, items = [SceneItem]()
  static let kinds = [
    ("orb", "Orbe"), ("spectrum", "Espectro"), ("card", "Tarjeta musical"), ("clock", "Reloj"),
    ("stars", "Estrellas"), ("mesh", "Malla animada"), ("image", "Imagen / GIF"), ("text", "Texto"),
    ("widget", "Widget HTML"),
  ]
  static let layoutKeys: Set<String> = [
    "sphereX", "sphereY", "radius", "spectrumX", "spectrumY", "spectrumWidth", "spectrumHeight",
    "cardX", "cardY", "cardscale", "clockX", "clockY", "clocksize",
  ]
  static func make(_ kind: String, _ c: Configuration, _ size: CGSize) -> SceneItem {
    var item = SceneItem()
    item.kind = kind
    item.gradients=c.gradients;item.name = L10n.text(kinds.first { $0.0 == kind }?.1 ?? kind)
    item.font = c.clockFont
    item.style = c.values.mapValues { .number($0) }.merging(c.colors.mapValues { .string($0) }) {
      _, new in new
    }
    let h =
      size.height
      * (kind == "spectrum"
        ? 0.6
        : kind == "orb"
          ? 0.32 : kind == "card" ? 0.18 : kind == "clock" || kind == "text" ? 0.08 : 0.35)
    let w =
      kind == "orb" ? h : size.width * (kind == "spectrum" ? 0.15 : kind == "card" ? 0.4 : 0.3)
    item.setRect(
      CGRect(x: (size.width - w) / 2, y: (size.height - h) / 2, width: w, height: h), size)
    return item
  }
  static func migrate(_ c: Configuration, _ size: CGSize) -> SceneDocument {
    var doc = SceneDocument()
    let w = size.width
    let h = size.height
    let r = h * c.n("radius") / 100
    func add(_ kind: String, _ rect: CGRect, _ visible: Bool, _ locked: Bool = false) {
      var item = make(kind, c, size)
      item.setRect(rect, size)
      item.visible = visible
      item.locked = locked
      doc.items.append(item)
    }
    add(
      c.n("bgstyle") < 0.5 ? "stars" : "mesh", CGRect(origin: .zero, size: size), c.on("bgshow"))
    add(
      "orb",
      CGRect(
        x: w * c.n("sphereX") / 100 - r, y: h * c.n("sphereY") / 100 - r, width: r * 2,
        height: r * 2), c.on("sphere"))
    add(
      "spectrum",
      CGRect(
        x: w * c.n("spectrumX") / 100, y: h * c.n("spectrumY") / 100,
        width: w * c.n("spectrumWidth") / 100, height: h * c.n("spectrumHeight") / 100),
      c.on("spectrum"))
    let scale = c.n("cardscale")
    let cw = min(850, max(260, w * 0.44)) * scale
    let ch = max(130, h * 0.21) * scale
    add(
      "card",
      CGRect(
        x: c.topCentered ? (w - cw) / 2 : w * c.n("cardX") / 100,
        y: c.topCentered ? h * c.topMargin / 100 : h - h * c.n("cardY") / 100 - ch, width: cw,
        height: ch), c.on("card"))
    let cs = c.n("clocksize")
    add(
      "clock",
      CGRect(
        x: max(0, w * c.n("clockX") / 100 - cs * 1.8),
        y: max(0, h * c.n("clockY") / 100 - cs * 0.7), width: cs * 3.6, height: cs * 1.4),
      c.on("clockshow"))
    return doc
  }
}
final class SceneRoot: NSView {
  override var isFlipped: Bool { true }
  let config: Configuration, audio: SystemAudio, media: NowPlaying
  private var nodes = [String: SceneNode](), subscription: AnyCancellable?
  private var orderedIDs=[String](),suspended=false
  init(frame: NSRect, config: Configuration, audio: SystemAudio, media: NowPlaying) {
    self.config = config
    self.audio = audio
    self.media = media
    super.init(frame: frame)
    wantsLayer = true
    subscription = config.objectWillChange.sink { [weak self] _ in
      DispatchQueue.main.async { self?.refresh() }
    }
    refresh()
  }
  required init?(coder: NSCoder) { fatalError() }
  override func layout() {
    super.layout()
    refresh()
  }
  func refresh() {
    guard let doc = config.scene else { return }
    let ids = Set(doc.items.map { $0.id })
    for (id, node) in nodes where !ids.contains(id) {
      node.removeFromSuperview()
      nodes.removeValue(forKey: id)
    }
    let orderChanged=orderedIDs != doc.items.map(\.id);orderedIDs=doc.items.map(\.id)
    var previous: NSView?
    for item in doc.items {
      let node: SceneNode
      if let existing = nodes[item.id] {
        node = existing
      } else {
        node = SceneNode(config: config, audio: audio, media: media)
        nodes[item.id] = node
      }
      let geometry=item.rect(bounds.size)
      if node.sceneRect != geometry || node.sceneAngle != item.rotation {node.frameCenterRotation=0;node.frame=geometry;node.sceneRect=geometry;node.sceneAngle=item.rotation}
      if node.superview !== self || orderChanged
      {
        addSubview(node, positioned: .above, relativeTo: previous)
      }
      node.apply(item)
      if node.frameCenterRotation != CGFloat(item.rotation){node.frameCenterRotation=CGFloat(item.rotation)}
      node.suspend(suspended)
      previous = node
    }
  }
  var frameCount:UInt64{nodes.values.compactMap{$0.renderer?.frameCount}.max() ?? 0}
  var fps:Double{nodes.values.compactMap{$0.renderer?.fps}.max() ?? 0}
  func pause(_ paused: Bool) { suspended=paused;for node in nodes.values { node.suspend(paused) } }
}
final class SceneNode: NSView {
  override var isFlipped: Bool { true }
  private var suspended=false
  func suspend(_ value:Bool){suspended=value;let offscreen=superview.map{parent in let area=metal.map{$0.convert($0.bounds,to:parent)} ?? convert(bounds,to:parent);return !area.intersects(parent.bounds)} ?? false;let paused=value || root.paused || isHidden || alphaValue<=0 || offscreen;widget?.suspend(paused);(child as? AnimatedTextView)?.pause(paused);if metal?.isPaused != paused{metal?.isPaused=paused};if let image=child as? NSImageView,image.animates == paused{image.animates = !paused}}
  var sceneRect=CGRect.null,sceneAngle=Double.nan
  let root: Configuration, proxy: Configuration, audio: SystemAudio, media: NowPlaying
  var metal: MTKView?, renderer: WallpaperRenderer?, widget: NativeWidget?
  private var item: SceneItem?, signature = "", child: NSView?, previousSize = CGSize.zero,
    previousFrame = CGRect.null
  init(config: Configuration, audio: SystemAudio, media: NowPlaying) {
    root = config
    proxy = config.preview()
    proxy.scene = nil
    self.audio = audio
    self.media = media
    super.init(frame: .zero)
    wantsLayer = true
    layer?.masksToBounds = true
  }
  required init?(coder: NSCoder) { fatalError() }
  func apply(_ input: SceneItem) {var next=input;next.cleanEffects()
    clipsToBounds = next.kind != "orb"
    layer?.masksToBounds = next.kind != "orb"
    if isHidden == next.visible{isHidden = !next.visible}
    if alphaValue != next.opacity{alphaValue = next.opacity}
    let signature = next.kind + "|" + (["image","widget"].contains(next.kind) ? next.asset : "")
    let rebuild = self.signature != signature
    var values=root.values,colors=root.colors
    for (key,value) in next.style where !SceneDocument.layoutKeys.contains(key){switch value{case .number(let n):values[key]=n;case .bool(let b):values[key]=b ? 1:0;case .string(let s):colors[key]=s}}
    if !rebuild {for key in SceneDocument.layoutKeys{values[key]=proxy.n(key)}}
    for key in ["sphere","spectrum","card","clockshow","bgshow"]{values[key]=0}
    values[next.kind == "orb" ? "sphere":next.kind == "spectrum" ? "spectrum":next.kind == "card" ? "card":next.kind == "clock" ? "clockshow":"bgshow"]=1
    values["bgstyle"]=next.kind == "mesh" ? 1:0
    for key in ["sensitivity","bass","gate","smoothing","fps","unlimitedfps","quality"]{values[key]=root.n(key)}
    if ["pulse","deformation","glow"].contains(where:{proxy.n($0) != (values[$0] ?? 0)}){previousFrame = .null}
    colors["backgroundcolor"]="#000000"
    if proxy.values != values{proxy.values=values};if proxy.colors != colors{proxy.colors=colors}
    if proxy.renderProfile != root.renderProfile{proxy.renderProfile=root.renderProfile}
    if proxy.clockFont != next.font{proxy.clockFont=next.font}
    if proxy.paused != root.paused{proxy.paused=root.paused}
    if rebuild {
      self.signature = signature;previousSize = .zero;previousFrame = .null
      child?.removeFromSuperview();metal?.isPaused=true;metal=nil;renderer=nil;widget=nil;child=nil
      proxy.scene=nil;proxy.backgroundPath=""
      if ["orb", "spectrum", "stars", "mesh"].contains(next.kind),
        let device = MTLCreateSystemDefaultDevice(),
        let renderer = try? WallpaperRenderer(
          config: proxy, analyzer: audio.analyzer, device: device, media: media)
      {
        let view = MTKView(frame: bounds, device: device)
        view.colorPixelFormat = .bgra8Unorm
        view.autoResizeDrawable = false
        view.framebufferOnly = true
        view.layer?.isOpaque = false
        view.clearColor = MTLClearColorMake(0, 0, 0, 0)
        renderer.transparent = true
        view.delegate = renderer
        self.renderer = renderer
        metal = view
        child = view
        proxy.values[
          next.kind == "orb" ? "sphere" : next.kind == "spectrum" ? "spectrum" : "bgshow"] = 1
        proxy.values["bgstyle"] = next.kind == "mesh" ? 1 : 0
      } else if next.kind == "image" {
        let view = NSImageView()
        view.image = NSImage(contentsOfFile: next.asset)
        view.imageScaling = .scaleAxesIndependently
        view.animates = true
        child = view
      } else if next.kind == "text" {
        let view = AnimatedTextView()
        view.update(next)
        child = view
      } else if next.kind == "clock" {
        proxy.values["clockshow"] = 1
        child = NSHostingView(rootView: ClockOverlay(config: proxy))
      } else if next.kind == "card" {
        proxy.values["card"] = 1
        child = NSHostingView(rootView: SceneMusicCard(config: proxy, media: media))
      } else if next.kind == "widget" {
        let view = NativeWidget(path: next.asset, audio: audio, media: media)
        widget = view
        child = view
      }
      if let child = child {
        child.frame = bounds
        child.autoresizingMask = [.width, .height]
        addSubview(child)
      }
    }
    if item?.font != next.font{previousSize = .zero}
    item = next
    if proxy.gradients != next.gradients{proxy.gradients=next.gradients}
    (child as? AnimatedTextView)?.update(next)
    suspend(suspended)
    if let text=child as? NSTextField {
      if text.stringValue != next.text{text.stringValue=next.text;previousSize = .zero}
      if text.textColor?.hex != NSColor(hex:next.color).hex{text.textColor=NSColor(hex:next.color)}
    }
    configureGeometry()
  }
  override func layout() {
    super.layout()
    configureGeometry()
  }
  private var previousRotation=Double.nan
  private func configureGeometry() {
    guard let item = item, bounds.size != previousSize || frame != previousFrame || item.rotation != previousRotation else { return }
    previousRotation=item.rotation
    previousFrame = frame
    previousSize = bounds.size
    if item.kind == "orb", let metal = metal {
      let radius = Double(min(bounds.width, bounds.height)) / 2
      let reach =
        radius * (1 + proxy.n("pulse")) * (1 + 0.38 * proxy.n("deformation")) + proxy.n("glow") + 4
      let viewport=superview?.bounds ?? frame.insetBy(dx:-reach,dy:-reach)
      let nominal=item.rect(superview?.bounds.size ?? bounds.size)
      var clip=viewport
      if item.rotation != 0 {
        let center=CGPoint(x:nominal.midX,y:nominal.midY),angle = -item.rotation * .pi / 180
        let points=[CGPoint(x:viewport.minX,y:viewport.minY),CGPoint(x:viewport.maxX,y:viewport.minY),CGPoint(x:viewport.minX,y:viewport.maxY),CGPoint(x:viewport.maxX,y:viewport.maxY)].map{p -> CGPoint in let x=p.x-center.x,y=p.y-center.y;return CGPoint(x:center.x+x*cos(angle)-y*sin(angle),y:center.y+x*sin(angle)+y*cos(angle))}
        let x=points.map(\.x),y=points.map(\.y);clip=CGRect(x:x.min()!,y:y.min()!,width:x.max()!-x.min()!,height:y.max()!-y.min()!)
      }
      var canvas=CGRect(x:nominal.midX-reach,y:nominal.midY-reach,width:reach*2,height:reach*2).intersection(clip)
      if canvas.isNull || canvas.width<1 || canvas.height<1 {canvas=CGRect(x:nominal.minX,y:nominal.minY,width:1,height:1)}
      canvas.origin.x-=nominal.minX;canvas.origin.y-=nominal.minY
      metal.autoresizingMask = []
      metal.frame = canvas
      proxy.values["sphereX"] = Double(bounds.midX - canvas.minX) / Double(canvas.width) * 100
      proxy.values["sphereY"] = Double(bounds.midY - canvas.minY) / Double(canvas.height) * 100
      proxy.values["radius"] = radius / Double(canvas.height) * 100
    }
    proxy.values["spectrumX"] = 0
    proxy.values["spectrumY"] = 0
    proxy.values["spectrumWidth"] = 100
    proxy.values["spectrumHeight"] = 100
    proxy.values["clockX"] = 50
    proxy.values["clockY"] = 50
    proxy.values["clocksize"] = max(8, Double(bounds.height) * 0.7)
    if let text = child as? NSTextField {
      text.font =
        item.font == "System"
        ? .systemFont(
          ofSize: max(
            8, bounds.height * 0.65 / Double(max(1, item.text.components(separatedBy: "\n").count)))
        ) : NSFont(name: item.font, size: max(8, bounds.height * 0.65))
    }
  }
}
struct SceneMusicCard: View {
  @ObservedObject var config: Configuration
  @ObservedObject var media: NowPlaying
  func stamp(_ time: Double) -> String {
    let s = max(0, Int(time))
    return String(format: "%d:%02d", s / 60, s % 60)
  }
  var body: some View {
    GeometryReader { g in
      let h = g.size.height
      let p = max(4, h * 0.1)
      VStack(spacing: p * 0.5) {
        HStack(spacing: p) {
          Group {
            if let cover = media.artwork {
              Image(nsImage: cover).resizable().aspectRatio(contentMode: .fill)
            } else {
              Image(systemName: "music.note").resizable().scaledToFit().padding(p)
            }
          }.frame(width: h * 0.55, height: h * 0.55).clipped().cornerRadius(p)
          VStack(alignment: .leading) {
            Text(media.title.isEmpty ? L10n.text("Audio del sistema") : media.title).font(
              .system(size: max(9, h * 0.15), weight: .bold))
            Text(media.artist).font(.system(size: max(8, h * 0.12)))
            Text(media.album).font(.system(size: max(7, h * 0.1))).foregroundStyle(.secondary)
          }.lineLimit(1).frame(maxWidth: .infinity, alignment: .leading)
          Text(media.provider=="Audio del sistema" ? L10n.text(media.provider):media.provider).font(.system(size: max(7, h * 0.09))).padding(6).background(
            .white.opacity(0.12), in: RoundedRectangle(cornerRadius: 6))
        }
        TimelineView(.periodic(from: .now, by: 1)) { _ in
          VStack(spacing: 4) {
            GeometryReader{p in ZStack(alignment:.leading){Capsule().fill(.white.opacity(0.18));Capsule().fill(config.paint("accent")).frame(width:p.size.width*(media.duration>0 ? min(1,media.elapsed()/media.duration):0))}}.frame(height:max(2,h*0.025))
            HStack {
              Text(stamp(media.elapsed()))
              Spacer()
              Text(media.duration > 0 ? "−" + stamp(media.duration - media.elapsed()) : "—")
            }.font(.system(size: max(7, h * 0.1)))
          }
        }
      }.padding(p).frame(width: g.size.width, height: g.size.height).background(
        .ultraThinMaterial, in: RoundedRectangle(cornerRadius: p * 1.4)
      ).overlay(RoundedRectangle(cornerRadius: p * 1.4).stroke(.white.opacity(0.25)))
        .foregroundStyle(.white).environment(\.colorScheme, .dark)
    }
  }
}
final class NativeWidget: WKWebView, WKNavigationDelegate {
  private var timer: Timer?, root: URL
  private var suspended=false
  func suspend(_ value:Bool){guard suspended != value else{return};suspended=value;if value{let zero="{\"bands\":["+Array(repeating:"0",count:64).joined(separator:",")+"],\"beat\":0,\"bpm\":0,\"playing\":false}";evaluateJavaScript("window.wirepulseAudio="+zero+";window.dispatchEvent(new CustomEvent('wirepulse:audio',{detail:window.wirepulseAudio}));",completionHandler:nil)}}
  init(path: String, audio: SystemAudio, media: NowPlaying) {
    root = URL(fileURLWithPath: path).deletingLastPathComponent()
    let config = WKWebViewConfiguration()
    config.mediaTypesRequiringUserActionForPlayback = .all
    // WebKit does not expose a public macOS switch for the white page backing.
    // Check the setter before using its compatibility KVC key (also used by Wry).
    if config.responds(to: NSSelectorFromString("_setDrawsBackground:"))
      || config.responds(to: NSSelectorFromString("setDrawsBackground:"))
    {
      config.setValue(false, forKey: "drawsBackground")
    }
    super.init(frame: .zero, configuration: config)
    navigationDelegate = self
    underPageBackgroundColor = .clear
    if responds(to: NSSelectorFromString("_setDrawsBackground:"))
      || responds(to: NSSelectorFromString("setDrawsBackground:"))
    {
      setValue(false, forKey: "drawsBackground")
    }
    loadFileURL(URL(fileURLWithPath: path), allowingReadAccessTo: root)
    timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) {
      [weak self, weak audio, weak media] _ in
      guard let self = self,!self.suspended,let s = audio?.analyzer.snapshot() else { return }
      let json: [String: Any] = [
        "bands": s.bands, "beat": s.age < 1 ? s.beat : 0,
        "bpm": (media?.tempoBpm ?? 0) > 0 ? media!.tempoBpm : s.bpm, "playing": s.age < 0.4,
      ]
      if let data = try? JSONSerialization.data(withJSONObject: json),
        let text = String(data: data, encoding: .utf8)
      {
        self.evaluateJavaScript(
          "window.wirepulseAudio=" + text
            + ";window.dispatchEvent(new CustomEvent('wirepulse:audio',{detail:window.wirepulseAudio}));",
          completionHandler: nil)
      }
    }
  }
  required init?(coder: NSCoder) { fatalError() }
  deinit { timer?.invalidate() }
  func webView(
    _ webView: WKWebView, decidePolicyFor action: WKNavigationAction,
    decisionHandler: @escaping (WKNavigationActionPolicy) -> Void
  ) {
    guard let url = action.request.url else {
      decisionHandler(.cancel)
      return
    }
    let safe =
      url.isFileURL && url.standardizedFileURL.path.hasPrefix(root.standardizedFileURL.path + "/")
    decisionHandler(action.targetFrame?.isMainFrame != false && !safe ? .cancel : .allow)
  }
}
