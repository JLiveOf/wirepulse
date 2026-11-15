import AppKit
import Combine
import MetalKit
import SwiftUI
import WebKit

struct SceneItem: Codable, Identifiable, Equatable {
  var id = UUID().uuidString, kind = "orb", name = "Orbe"
  var x = 0.35, y = 0.3, width = 0.3, height = 0.4
  var visible = true, locked = false, keepAspect = true, opacity = 1.0
  var asset = "", text = "Tu texto", font = "System", color = "#ffffff"
  var style = [String: SceneValue]()
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
    item.name = kinds.first { $0.0 == kind }?.1 ?? kind
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
      c.n("bgstyle") < 0.5 ? "stars" : "mesh", CGRect(origin: .zero, size: size), c.on("bgshow"),
      true)
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
    var previous: NSView?
    for item in doc.items {
      let node: SceneNode
      if let existing = nodes[item.id] {
        node = existing
      } else {
        node = SceneNode(config: config, audio: audio, media: media)
        nodes[item.id] = node
      }
      node.frame = item.rect(bounds.size)
      node.apply(item)
      if node.superview !== self
        || subviews.firstIndex(of: node) != doc.items.firstIndex(where: { $0.id == item.id })
      {
        addSubview(node, positioned: .above, relativeTo: previous)
      }
      previous = node
    }
  }
  func pause(_ paused: Bool) { for node in nodes.values { node.metal?.isPaused = paused } }
}
final class SceneNode: NSView {
  override var isFlipped: Bool { true }
  let root: Configuration, proxy: Configuration, audio: SystemAudio, media: NowPlaying
  var metal: MTKView?, renderer: WallpaperRenderer?, widget: NativeWidget?
  private var item: SceneItem?, signature = "", child: NSView?, previousSize = CGSize.zero
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
  func apply(_ next: SceneItem) {
    isHidden = !next.visible
    alphaValue = next.opacity
    let encoder = JSONEncoder()
    encoder.outputFormatting = .sortedKeys
    let signature =
      String(data: (try? encoder.encode(next.style)) ?? Data(), encoding: .utf8)! + next.kind
      + next.asset + next.text + next.font + next.color
    if self.signature != signature {
      self.signature = signature
      previousSize = .zero
      child?.removeFromSuperview()
      metal?.isPaused = true
      metal = nil
      renderer = nil
      widget = nil
      child = nil
      proxy.copyLayout(from: root)
      proxy.scene = nil
      proxy.backgroundPath = ""
      proxy.colors["backgroundcolor"] = "#000000"
      for (key, value) in next.style {
        switch value {
        case .number(let n): proxy.values[key] = n
        case .bool(let b): proxy.values[key] = b ? 1 : 0
        case .string(let s): proxy.colors[key] = s
        }
      }
      for key in ["sphere", "spectrum", "card", "clockshow", "bgshow"] { proxy.values[key] = 0 }
      proxy.clockFont = next.font
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
        let view = NSTextField(wrappingLabelWithString: next.text)
        view.alignment = .center
        view.textColor = NSColor(hex: next.color)
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
    item = next
    metal?.isPaused = !next.visible
    for key in ["sensitivity", "bass", "gate", "smoothing", "fps", "unlimitedfps", "quality"] {
      proxy.values[key] = root.n(key)
    }
    proxy.paused = root.paused
    configureGeometry()
  }
  override func layout() {
    super.layout()
    configureGeometry()
  }
  private func configureGeometry() {
    guard let item = item, bounds.size != previousSize else { return }
    previousSize = bounds.size
    proxy.values["sphereX"] = 50
    proxy.values["sphereY"] = 50
    proxy.values["radius"] = max(
      1, Double(min(bounds.width, bounds.height)) * 0.46 / max(1, Double(bounds.height)) * 100)
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
            Text(media.title.isEmpty ? "Audio del sistema" : media.title).font(
              .system(size: max(9, h * 0.15), weight: .bold))
            Text(media.artist).font(.system(size: max(8, h * 0.12)))
            Text(media.album).font(.system(size: max(7, h * 0.1))).foregroundStyle(.secondary)
          }.lineLimit(1).frame(maxWidth: .infinity, alignment: .leading)
          Text(media.provider).font(.system(size: max(7, h * 0.09))).padding(6).background(
            .white.opacity(0.12), in: RoundedRectangle(cornerRadius: 6))
        }
        TimelineView(.periodic(from: .now, by: 1)) { _ in
          VStack(spacing: 4) {
            ProgressView(value: media.duration > 0 ? min(1, media.elapsed() / media.duration) : 0)
              .tint(Color(nsColor: config.color("accent")))
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
  init(path: String, audio: SystemAudio, media: NowPlaying) {
    root = URL(fileURLWithPath: path).deletingLastPathComponent()
    let config = WKWebViewConfiguration()
    config.mediaTypesRequiringUserActionForPlayback = .all
    super.init(frame: .zero, configuration: config)
    navigationDelegate = self
    underPageBackgroundColor = .clear
    loadFileURL(URL(fileURLWithPath: path), allowingReadAccessTo: root)
    timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) {
      [weak self, weak audio, weak media] _ in
      guard let self = self, let s = audio?.analyzer.snapshot() else { return }
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
