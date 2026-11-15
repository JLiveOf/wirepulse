import AppKit
import Combine
import SwiftUI
import UniformTypeIdentifiers

final class SceneEditorModel: ObservableObject {
  let original: Configuration, draft: Configuration, size: CGSize
  weak var window: NSWindow?
  @Published var selected: String?
  @Published var snap: Bool
  @Published var inspector = true
  private var undo = [SceneDocument](), redo = [SceneDocument]()
  var document: SceneDocument {
    get { draft.scene! }
    set {
      draft.scene = newValue
      objectWillChange.send()
    }
  }
  var item: SceneItem? { document.items.first { $0.id == selected } }
  init(_ config: Configuration, _ size: CGSize) {
    original = config
    draft = config.preview()
    self.size = size
    snap = config.editorSnap
    draft.scene = config.scene ?? SceneDocument.migrate(config, size)
    selected = draft.scene?.items.first { $0.kind == "card" && $0.visible }?.id
  }
  func checkpoint() {
    undo.append(document)
    if undo.count > 80 { undo.removeFirst() }
    redo = []
  }
  func update(_ change: (inout SceneItem) -> Void) {
    guard let i = document.items.firstIndex(where: { $0.id == selected }) else { return }
    var doc = document
    change(&doc.items[i])
    document = doc
  }
  func change(_ action: (inout SceneItem) -> Void) {
    checkpoint()
    update(action)
  }
  func history(_ backwards: Bool) {
    if backwards {
      guard let doc = undo.popLast() else { return }
      redo.append(document)
      document = doc
    } else {
      guard let doc = redo.popLast() else { return }
      undo.append(document)
      document = doc
    }
  }
  func save() {
    original.editorSnap = snap
    original.scene = document
  }
  func duplicate() {
    guard var item = item else { return }
    checkpoint()
    item.id = UUID().uuidString
    item.name += " · copia"
    item.x += 0.02
    item.y += 0.02
    item.locked = false
    document.items.append(item)
    selected = item.id
  }
  func delete() {
    guard let item = item, !item.locked else { return }
    checkpoint()
    document.items.removeAll { $0.id == item.id }
    selected = nil
  }
  func add(_ kind: String) {
    guard document.items.count < 256 else { return }
    var item = SceneDocument.make(kind, draft, size)
    if kind == "image" || kind == "widget" {
      let panel = NSOpenPanel()
      panel.title = kind == "image" ? "Añadir imagen o GIF" : "Añadir widget HTML"
      panel.prompt = "Añadir"
      defer { window?.makeKeyAndOrderFront(nil) }
      panel.canChooseDirectories = kind == "widget"
      panel.canChooseFiles = true
      panel.allowsMultipleSelection = false
      panel.allowedContentTypes = kind == "image" ? [.image] : [.html, .zip]
      guard panel.runModal() == .OK, let url = panel.url else { return }
      do {
        item.asset = try SceneAssets.importAsset(url, widget: kind == "widget").path
        item.name = url.deletingPathExtension().lastPathComponent
        if kind == "image", let image = NSImage(contentsOfFile: item.asset), image.size.width > 0 {
          let ratio = image.size.height / image.size.width
          item.height = item.width * size.width * ratio / size.height
        }
      } catch {
        NSAlert(error: error).runModal()
        return
      }
    }
    checkpoint()
    document.items.append(item)
    selected = item.id
  }
}
enum SceneAssets {
  static func importAsset(_ source: URL, widget: Bool) throws -> URL {
    let fm = FileManager.default
    let folder = fm.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
      .appendingPathComponent("WirePulse/Assets/" + UUID().uuidString, isDirectory: true)
    try fm.createDirectory(at: folder, withIntermediateDirectories: true)
    do {
      let attrs = try fm.attributesOfItem(atPath: source.path)
      if attrs[.type] as? FileAttributeType == .typeDirectory {
        try validate(source)
        let dest = folder.appendingPathComponent("widget")
        try fm.copyItem(at: source, to: dest)
        return try entry(dest)
      }
      if source.pathExtension.lowercased() == "zip" {
        try validateZip(source)
        let listing = Process()
        listing.executableURL = URL(fileURLWithPath: "/usr/bin/unzip")
        listing.arguments = ["-Z1", source.path]
        let pipe = Pipe()
        listing.standardOutput = pipe
        listing.standardError = FileHandle.nullDevice
        try listing.run()
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        listing.waitUntilExit()
        let paths =
          String(data: data, encoding: .utf8)?.split(separator: "\n").map(String.init) ?? []
        guard listing.terminationStatus == 0, paths.count <= 2048, !paths.isEmpty,
          paths.allSatisfy({
            !$0.hasPrefix("/") && !$0.contains("\\") && !$0.split(separator: "/").contains("..")
          })
        else { throw failure("El ZIP contiene rutas no válidas.") }
        let unzip = Process()
        unzip.executableURL = URL(fileURLWithPath: "/usr/bin/unzip")
        unzip.arguments = ["-qq", source.path, "-d", folder.path]
        unzip.standardError = FileHandle.nullDevice
        try unzip.run()
        unzip.waitUntilExit()
        guard unzip.terminationStatus == 0 else { throw failure("No se pudo abrir el ZIP.") }
        try validate(folder)
        return try entry(folder)
      }
      guard (attrs[.size] as? NSNumber)?.intValue ?? 0 <= 128 * 1024 * 1024 else {
        throw failure("El archivo es demasiado grande.")
      }
      let dest = folder.appendingPathComponent(source.lastPathComponent)
      try fm.copyItem(at: source, to: dest)
      return dest
    } catch {
      try? fm.removeItem(at: folder)
      throw error
    }
  }
  static func validateZip(_ source: URL) throws {
    let data = try Data(contentsOf: source, options: .mappedIfSafe)
    guard data.count <= 128 * 1024 * 1024, data.count >= 22 else {
      throw failure("ZIP demasiado grande o incompleto.")
    }
    func u16(_ i: Int) -> Int { Int(data[i]) | Int(data[i + 1]) << 8 }
    func u32(_ i: Int) -> Int { u16(i) | u16(i + 2) << 16 }
    guard
      let end = stride(from: data.count - 22, through: max(0, data.count - 65557), by: -1).first(
        where: { u32($0) == 0x0605_4b50 })
    else { throw failure("ZIP no compatible.") }
    let count = u16(end + 10)
    var cursor = u32(end + 16)
    var total = 0
    guard count > 0, count <= 2048 else { throw failure("Demasiados archivos.") }
    for _ in 0..<count {
      guard cursor >= 0, cursor + 46 <= data.count, u32(cursor) == 0x0201_4b50 else {
        throw failure("Índice ZIP no válido.")
      }
      let length = u16(cursor + 28)
      let extra = u16(cursor + 30)
      let comment = u16(cursor + 32)
      let attributes = u32(cursor + 38)
      guard cursor + 46 + length + extra + comment <= data.count,
        let name = String(
          data: data.subdata(in: (cursor + 46)..<(cursor + 46 + length)), encoding: .utf8),
        !name.hasPrefix("/"), !name.contains("\\"), !name.contains("\0"),
        !name.split(separator: "/").contains(".."), (attributes >> 16) & 0xf000 != 0xa000
      else { throw failure("El ZIP contiene enlaces o rutas no válidas.") }
      total += u32(cursor + 24)
      guard total <= 128 * 1024 * 1024 else {
        throw failure("El contenido del ZIP es demasiado grande.")
      }
      cursor += 46 + length + extra + comment
    }
  }
  static func failure(_ text: String) -> NSError {
    NSError(domain: "WirePulse", code: 1, userInfo: [NSLocalizedDescriptionKey: text])
  }
  static func validate(_ folder: URL) throws {
    let fm = FileManager.default
    guard
      let files = fm.enumerator(
        at: folder, includingPropertiesForKeys: [.isSymbolicLinkKey, .fileSizeKey])
    else { throw failure("No se puede leer la carpeta.") }
    var count = 0
    var total = 0
    for case let url as URL in files {
      let values = try url.resourceValues(forKeys: [.isSymbolicLinkKey, .fileSizeKey])
      count += 1
      total += values.fileSize ?? 0
      if values.isSymbolicLink == true || count > 2048 || total > 128 * 1024 * 1024 {
        throw failure("Paquete demasiado grande o con enlaces no compatibles.")
      }
    }
  }
  static func entry(_ folder: URL) throws -> URL {
    let files =
      (FileManager.default.enumerator(at: folder, includingPropertiesForKeys: nil)?.allObjects
      as? [URL] ?? []).filter { ["html", "htm"].contains($0.pathExtension.lowercased()) }.sorted {
        a, b in
        if (a.lastPathComponent == "index.html") != (b.lastPathComponent == "index.html") {
          return a.lastPathComponent == "index.html"
        }
        return a.path.count == b.path.count ? a.path < b.path : a.path.count < b.path.count
      }
    guard let first = files.first else { throw failure("El widget necesita index.html.") }
    return first
  }
}
final class EditorCanvasView: NSView {
  override var isFlipped: Bool { true }
  override var acceptsFirstResponder: Bool { true }
  let model: SceneEditorModel
  var start = CGPoint.zero, initial = CGRect.zero, handle = -1, dragging = false
  init(_ model: SceneEditorModel) {
    self.model = model
    super.init(frame: CGRect(origin: .zero, size: model.size))
  }
  required init?(coder: NSCoder) { fatalError() }
  static func handles(_ r: CGRect) -> [CGPoint] {
    [
      CGPoint(x: r.minX, y: r.minY), CGPoint(x: r.midX, y: r.minY), CGPoint(x: r.maxX, y: r.minY),
      CGPoint(x: r.maxX, y: r.midY), CGPoint(x: r.maxX, y: r.maxY), CGPoint(x: r.midX, y: r.maxY),
      CGPoint(x: r.minX, y: r.maxY), CGPoint(x: r.minX, y: r.midY),
    ]
  }
  override func draw(_ dirtyRect: NSRect) {
    guard let item = model.item, item.visible else { return }
    let r = item.rect(bounds.size)
    (item.locked ? NSColor.systemOrange : NSColor.systemCyan).setStroke()
    let outline = outlinePath(r, model.item?.rotation ?? 0)
    outline.lineWidth = 2
    outline.stroke()
    if !item.locked {
      for p in Self.handles(r).map({ Self.rotate($0, CGPoint(x: r.midX, y: r.midY), item.rotation) }
      ) {
        NSColor.white.setFill()
        NSBezierPath(rect: CGRect(x: p.x - 4, y: p.y - 4, width: 8, height: 8)).fill()
      }
    }
  }
  static func rotate(_ p: CGPoint, _ c: CGPoint, _ degrees: Double) -> CGPoint {
    let a = degrees * Double.pi / 180
    let co = cos(a)
    let si = sin(a)
    let x = Double(p.x - c.x)
    let y = Double(p.y - c.y)
    return CGPoint(x: Double(c.x) + x * co - y * si, y: Double(c.y) + x * si + y * co)
  }
  func outlinePath(_ r: CGRect, _ angle: Double) -> NSBezierPath {
    let p = NSBezierPath()
    let corners = [
      CGPoint(x: r.minX, y: r.minY), CGPoint(x: r.maxX, y: r.minY), CGPoint(x: r.maxX, y: r.maxY),
      CGPoint(x: r.minX, y: r.maxY),
    ].map { Self.rotate($0, CGPoint(x: r.midX, y: r.midY), angle) }
    p.move(to: corners[0])
    for c in corners.dropFirst() { p.line(to: c) }
    p.close()
    return p
  }
  override func mouseDown(with event: NSEvent) {
    window?.makeFirstResponder(self)
    let p = convert(event.locationInWindow, from: nil)
    handle = -1
    if let item = model.item, !item.locked {
      handle =
        Self.handles(item.rect(bounds.size)).map {
          Self.rotate(
            $0, CGPoint(x: item.rect(bounds.size).midX, y: item.rect(bounds.size).midY),
            item.rotation)
        }.firstIndex { hypot($0.x - p.x, $0.y - p.y) < 11 } ?? -1
    }
    if handle < 0 {
      model.selected =
        model.document.items.last {
          $0.visible && !$0.locked
            && $0.rect(bounds.size).contains(
              Self.rotate(
                p, CGPoint(x: $0.rect(bounds.size).midX, y: $0.rect(bounds.size).midY), -$0.rotation
              ))
        }?
        .id
    }
    guard let item = model.item, !item.locked else {
      needsDisplay = true
      return
    }
    start = p
    initial = item.rect(bounds.size)
    dragging = true
    model.checkpoint()
    needsDisplay = true
  }
  override func mouseDragged(with event: NSEvent) {
    guard dragging, let item = model.item else { return }
    let p = convert(event.locationInWindow, from: nil)
    let dx = p.x - start.x
    let dy = p.y - start.y
    var r = initial
    if handle < 0 {
      r.origin.x += dx
      r.origin.y += dy
      if model.snap && !event.modifierFlags.contains(.option) {
        let others = model.document.items.filter { $0.id != item.id && $0.visible }.map {
          $0.rect(bounds.size)
        }
        let xs: [CGFloat] =
          [0, bounds.midX, bounds.maxX] + others.flatMap { [$0.minX, $0.midX, $0.maxX] }
        let ys: [CGFloat] =
          [0, bounds.midY, bounds.maxY] + others.flatMap { [$0.minY, $0.midY, $0.maxY] }
        r.origin.x += nearest([r.minX, r.midX, r.maxX], xs)
        r.origin.y += nearest([r.minY, r.midY, r.maxY], ys)
      }
      r.origin.x = max(min(0, 24 - r.width), min(bounds.width - 24, r.minX))
      r.origin.y = max(min(0, 24 - r.height), min(bounds.height - 24, r.minY))
    } else {
      let local = Self.rotate(CGPoint(x: dx, y: dy), .zero, -item.rotation)
      let dx = local.x
      let dy = local.y
      var left = initial.minX
      var top = initial.minY
      var right = initial.maxX
      var bottom = initial.maxY
      if [0, 6, 7].contains(handle) { left += dx }
      if [2, 3, 4].contains(handle) { right += dx }
      if [0, 1, 2].contains(handle) { top += dy }
      if [4, 5, 6].contains(handle) { bottom += dy }
      var w = max(16, right - left)
      var h = max(16, bottom - top)
      if item.keepAspect && !event.modifierFlags.contains(.option) {
        let ratio = initial.width / initial.height
        if abs(dx / initial.width) > abs(dy / initial.height) {
          h = w / ratio
        } else {
          w = h * ratio
        }
        if [0, 6, 7].contains(handle) { left = initial.maxX - w }
        if [0, 1, 2].contains(handle) { top = initial.maxY - h }
      }
      r = CGRect(x: left, y: top, width: w, height: h)
      let old = Self.rotate(
        Self.handles(initial)[(handle + 4) % 8], CGPoint(x: initial.midX, y: initial.midY),
        item.rotation)
      let new = Self.rotate(
        Self.handles(r)[(handle + 4) % 8], CGPoint(x: r.midX, y: r.midY), item.rotation)
      r.origin.x += old.x - new.x
      r.origin.y += old.y - new.y
    }
    model.update { $0.setRect(r, bounds.size) }
    needsDisplay = true
  }
  func nearest(_ edges: [CGFloat], _ targets: [CGFloat]) -> CGFloat {
    var offset: CGFloat = 0
    var best: CGFloat = 8
    for t in targets {
      for e in edges where abs(t - e) < best {
        best = abs(t - e)
        offset = t - e
      }
    }
    return offset
  }
  override func mouseUp(with event: NSEvent) { dragging = false }
  override func keyDown(with event: NSEvent) {
    if event.modifierFlags.contains(.command) {
      switch event.charactersIgnoringModifiers {
      case "z": model.history(!event.modifierFlags.contains(.shift))
      case "d": model.duplicate()
      default: super.keyDown(with: event)
      }
    } else if event.keyCode == 51 || event.keyCode == 117 {
      model.delete()
    } else {
      super.keyDown(with: event)
    }
    needsDisplay = true
  }
}
struct EditorChrome: View {
  @ObservedObject var model: SceneEditorModel
  let finish: (Bool) -> Void
  var availableHeight: CGFloat? = nil
  var body: some View {
    ZStack(alignment: .topTrailing) {
      if model.inspector {
        VStack(alignment: .leading, spacing: 10) {
          Text("Objetos").font(.title2.bold())
          Text("Arrastra · esquinas para redimensionar · ⌥ sin guías").font(.caption)
          ScrollView {
            LazyVStack(alignment: .leading, spacing: 4) {
              ForEach(model.document.items.reversed()) { item in
                HStack(spacing: 4) {
                  Button {
                    model.selected = item.id
                  } label: {
                    HStack {
                      Text((item.visible ? "● " : "◌ ") + item.name).lineLimit(1)
                      Spacer()
                    }.padding(8).contentShape(Rectangle())
                  }.buttonStyle(.plain)
                  Button {
                    model.selected = item.id
                    model.change { $0.locked.toggle() }
                  } label: {
                    Image(systemName: item.locked ? "lock.fill" : "lock.open")
                  }.buttonStyle(.borderless).help(
                    item.locked ? "Desbloquear objeto" : "Bloquear objeto"
                  ).accessibilityLabel((item.locked ? "Desbloquear " : "Bloquear ") + item.name)
                    .padding(.trailing, 8)
                }.background(
                  model.selected == item.id ? Color.accentColor.opacity(0.55) : Color.clear,
                  in: RoundedRectangle(cornerRadius: 8)
                ).accessibilityAddTraits(model.selected == item.id ? .isSelected : [])
              }
            }
          }.frame(height: 180)
          ScrollView { properties }
        }.padding().frame(width: 290).frame(maxHeight: max(160, (availableHeight ?? model.size.height) - 120)).background(
          .regularMaterial, in: RoundedRectangle(cornerRadius: 14)
        ).padding()
      }
      VStack {
        Spacer()
        HStack {
          Menu("＋ Añadir") {
            ForEach(SceneDocument.kinds, id: \.0) { kind in Button(kind.1) { model.add(kind.0) } }
          }
          Button("↶") { model.history(true) }
          Button("↷") { model.history(false) }
          Toggle("Ajustar a guías", isOn: $model.snap).fixedSize()
          Button("Eliminar objeto") { model.delete() }.disabled(
            model.item == nil || model.item?.locked == true)
          Button("Propiedades") { model.inspector.toggle() }
          Button("Cancelar") { finish(false) }
          Button("Guardar diseño") { finish(true) }.buttonStyle(.borderedProminent)
        }.padding(12).background(.regularMaterial, in: RoundedRectangle(cornerRadius: 14)).padding()
      }.frame(maxWidth: .infinity, maxHeight: .infinity)
    }
  }
  @ViewBuilder var properties: some View {
    if let item = model.item {
      VStack(alignment: .leading) {
        TextField("Nombre", text: binding(item.name) { $0.name = $1 })
        Toggle("Visible", isOn: binding(item.visible) { $0.visible = $1 })
        Toggle("Bloquear posición", isOn: binding(item.locked) { $0.locked = $1 })
        Toggle("Mantener proporciones", isOn: binding(item.keepAspect) { $0.keepAspect = $1 })
        TextField(
          "Ángulo (grados)", value: binding(item.rotation) { $0.rotation = $1 }, format: .number)
        HStack {
          Button("↶ 90°") { model.change { $0.rotation -= 90 } }
          Button("↷ 90°") { model.change { $0.rotation += 90 } }
          Button("Sin giro") { model.change { $0.rotation = 0 } }
        }
        HStack {
          Button("Duplicar") { model.duplicate() }
          Button("Eliminar") { model.delete() }
        }
        HStack {
          Button("Centrar") {
            model.change {
              $0.x = (1 - $0.width) / 2
              $0.y = (1 - $0.height) / 2
            }
          }
          Button("Encajar") {
            model.change {
              var r = $0.rect(model.size)
              let scale = min(
                model.size.width * 0.85 / r.width, model.size.height * 0.85 / r.height)
              r.size.width *= scale
              r.size.height *= scale
              r.origin = CGPoint(
                x: (model.size.width - r.width) / 2, y: (model.size.height - r.height) / 2)
              $0.setRect(r, model.size)
            }
          }
        }
        HStack {
          Button("Al frente") {
            model.checkpoint()
            model.document.items.removeAll { $0.id == item.id }
            model.document.items.append(item)
          }
          Button("Al fondo") {
            model.checkpoint()
            model.document.items.removeAll { $0.id == item.id }
            model.document.items.insert(item, at: 0)
          }
        }
        if item.kind == "text" {
          TextEditor(text: binding(item.text) { $0.text = $1 }).frame(height: 80)
          ColorPicker(
            "Color",
            selection: Binding(
              get: { Color(nsColor: NSColor(hex: model.item?.color ?? "#ffffff")) },
              set: { color in model.change { $0.color = NSColor(color).hex } }),
            supportsOpacity: false)
        }
        if item.kind == "text" || item.kind == "clock" {
          Picker("Fuente", selection: binding(item.font) { $0.font = $1 }) {
            Text("Sistema").tag("System")
            ForEach(NSFontManager.shared.availableFontFamilies.sorted(), id: \.self) {
              Text($0).tag($0)
            }
          }
        }
        let group =
          item.kind == "orb"
          ? "Orbe"
          : item.kind == "spectrum"
            ? "Espectro"
            : item.kind == "card" ? "Tarjeta" : item.kind == "clock" ? "Reloj" : "Fondo animado"
        if !["text", "image", "widget"].contains(item.kind) {
          ForEach(
            controls.filter {
              $0.group == group && !SceneDocument.layoutKeys.contains($0.id)
                && !["sphere", "spectrum", "card", "clockshow", "bgshow"].contains($0.id)
            }
          ) { spec in style(spec) }
        }
        Text("Opacidad")
        Slider(value: binding(item.opacity) { $0.opacity = $1 }, in: 0...1)
      }
    } else {
      Text("Selecciona un objeto para editarlo.")
    }
  }
  func binding<T>(_ fallback: T, _ change: @escaping (inout SceneItem, T) -> Void) -> Binding<T> {
    Binding(get: { fallback }, set: { value in model.change { change(&$0, value) } })
  }
  @ViewBuilder func style(_ spec: ControlSpec) -> some View {
    switch spec.kind {
    case .color:
      ColorPicker(
        spec.title,
        selection: Binding(
          get: {
            if case .string(let s) = model.item?.style[spec.id] {
              return Color(nsColor: NSColor(hex: s))
            }
            return .white
          }, set: { value in model.change { $0.style[spec.id] = .string(NSColor(value).hex) } }),
        supportsOpacity: false)
    case .toggle:
      Toggle(
        spec.title,
        isOn: Binding(
          get: {
            if case .number(let n) = model.item?.style[spec.id] { return n > 0.5 }
            if case .bool(let b) = model.item?.style[spec.id] { return b }
            return false
          }, set: { value in model.change { $0.style[spec.id] = .number(value ? 1 : 0) } }))
    case .slider(let lo, let hi, let step):
      Text(spec.title)
      Slider(
        value: Binding(
          get: {
            if case .number(let n) = model.item?.style[spec.id] { return min(hi, max(lo, n)) }
            return lo
          }, set: { value in model.change { $0.style[spec.id] = .number(value) } }), in: lo...hi,
        step: step)
    case .image: EmptyView()
    }
  }
}
final class SceneEditorWindow: NSWindow {
  let model: SceneEditorModel
  var subscription: AnyCancellable?
  private let initial: SceneDocument
  init(screen: NSScreen, config: Configuration, audio: SystemAudio, media: NowPlaying) {
    model = SceneEditorModel(config, screen.frame.size)
    initial = model.document
    super.init(
      contentRect: screen.frame, styleMask: [.borderless], backing: .buffered, defer: false)
    model.window = self
    isReleasedWhenClosed = false
    level = .floating
    title = "WirePulse · Editar escritorio"
    let root = NSView(frame: CGRect(origin: .zero, size: screen.frame.size))
    root.wantsLayer = true
    root.layer?.backgroundColor = config.color("backgroundcolor").cgColor
    root.layer?.contents = NSImage(contentsOfFile: config.backgroundPath)?.cgImage(
      forProposedRect: nil, context: nil, hints: nil)
    root.layer?.contentsGravity = .resizeAspectFill
    if !config.backgroundPath.isEmpty {
      let dim = NSView(frame: root.bounds)
      dim.wantsLayer = true
      dim.layer?.backgroundColor = NSColor.black.withAlphaComponent(config.n("dim") / 100).cgColor
      root.addSubview(dim)
    }
    contentView = root
    let scene = SceneRoot(frame: root.bounds, config: model.draft, audio: audio, media: media)
    root.addSubview(scene)
    let canvas = EditorCanvasView(model)
    root.addSubview(canvas)
    let chrome = EditorHostingView(
      rootView: EditorChrome(model: model, finish: { [weak self] save in self?.finish(save) }, availableHeight: screen.visibleFrame.height))
    chrome.frame = Self.controlsFrame(screen: screen.frame, visible: screen.visibleFrame)
    root.addSubview(chrome)
    subscription = model.objectWillChange.sink { [weak canvas] _ in
      DispatchQueue.main.async { canvas?.needsDisplay = true }
    }
  }
  static func controlsFrame(screen: CGRect, visible: CGRect) -> CGRect {
    let safe = screen.intersection(visible)
    return safe.offsetBy(dx: -screen.minX, dy: -screen.minY)
  }
  override var canBecomeKey: Bool { true }
  override var canBecomeMain: Bool { true }
  func finish(_ save: Bool) {
    if !save && model.document != initial {
      let alert = NSAlert()
      alert.messageText = "¿Descartar los cambios del diseño?"
      alert.addButton(withTitle: "Seguir editando")
      alert.addButton(withTitle: "Descartar")
      if alert.runModal() != .alertSecondButtonReturn { return }
    }
    if save { model.save() }
    orderOut(nil)
    close()
  }
}

final class EditorHostingView: NSHostingView<EditorChrome> {
  override func hitTest(_ point: NSPoint) -> NSView? {
    let p = convert(point, from: superview)
    let right = rootView.model.inspector && p.x > bounds.width - 330
    let bottom = isFlipped ? p.y > bounds.height - 90 : p.y < 90
    return right || bottom ? super.hitTest(point) : nil
  }
}
