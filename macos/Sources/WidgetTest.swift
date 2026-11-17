import AppKit
import WebKit

/// Real WebKit load, live audio event and snapshot integration; never touches saved preferences.
func testNativeWidget(path: String) -> Int32 {
  _ = NSApplication.shared
  NSApp.setActivationPolicy(.accessory)
  NSApp.finishLaunching()
  let audio = SystemAudio()
  let media = NowPlaying()
  let widget = NativeWidget(path: path, audio: audio, media: media)
  let window = NSWindow(
    contentRect: NSRect(x: 64, y: 64, width: 400, height: 240), styleMask: .borderless,
    backing: .buffered, defer: false)
  window.isReleasedWhenClosed = false
  window.contentView = widget
  window.orderFrontRegardless()
  defer { withExtendedLifetime((audio, media)) {} }
  var done = false
  var busy = false
  var checkingAlpha = false
  var code: Int32 = 1
  var diagnostic = "no callback"
  let deadline = Date().addingTimeInterval(25)
  let timer = Timer.scheduledTimer(withTimeInterval: 0.25, repeats: true) { _ in
    if busy || done || checkingAlpha { return }
    busy = true
    widget.evaluateJavaScript("window.wirepulseAudio && window.wirepulseAudio.bands.length===64") {
      value, error in
      diagnostic = "audio=\(String(describing:value)) error=\(String(describing:error))"
      guard (value as? Bool) == true else {
        if let error = error { fputs("WebKit JS: \(error)\n", stderr) }
        busy = false
        return
      }
      widget.evaluateJavaScript(
        "window.dispatchEvent(new CustomEvent('wirepulse:audio',{detail:{bands:Array(64).fill(0.5),beat:0.8,bpm:120,playing:true}}));"
      ) { _, _ in
        widget.takeSnapshot(with: nil) { image, error in
          defer { busy = false }
          guard let image = image, let data = image.tiffRepresentation,
            let bitmap = NSBitmapImageRep(data: data),
            let color = bitmap.colorAt(x: bitmap.pixelsWide / 2, y: bitmap.pixelsHigh / 2)?
              .usingColorSpace(.sRGB)
          else { return }
          diagnostic = "pixel=\(color.redComponent),\(color.greenComponent),\(color.blueComponent)"
          if color.redComponent < 0.25 && color.greenComponent > 0.45 && color.blueComponent > 0.7 {
            print("PASS native WebKit widget load, live audio event and rendered pixels")
            busy = true
            checkingAlpha = true
            widget.evaluateJavaScript(
              "document.documentElement.style.background='transparent';document.body.style.background='transparent';"
            ) { _, _ in
              widget.takeSnapshot(with: nil) { image, error in
                defer {
                  busy = false
                  done = true
                }
                guard let data = image?.tiffRepresentation,
                  let bitmap = NSBitmapImageRep(data: data), let corner = bitmap.colorAt(x: 2, y: 2)
                else { return }
                if corner.alphaComponent < 0.05 {
                  code = 0
                  print("PASS native WebKit preserves transparent pixels")
                } else {
                  diagnostic = "Transparent HTML became opaque"
                }
              }
            }
          }
        }
      }
    }
  }
  while !done && Date() < deadline { RunLoop.main.run(until: Date().addingTimeInterval(0.05)) }
  timer.invalidate()
  var pauseChecked=false
  widget.suspend(true)
  widget.evaluateJavaScript("window.wirepulseAudio.playing===false && window.wirepulseAudio.beat===0 && window.wirepulseAudio.bands.every(x=>x===0)"){value,error in if value as? Bool != true{code=1;diagnostic="Paused widget still receives active audio"};pauseChecked=true}
  let pauseDeadline=Date().addingTimeInterval(3)
  while !pauseChecked && Date()<pauseDeadline{RunLoop.main.run(until:Date().addingTimeInterval(0.05))}
  if !pauseChecked{code=1}else if code==0{print("PASS native widget suspension publishes silence and stops live audio updates")}
  widget.suspend(false)
  window.orderOut(nil)
  widget.stopLoading()
  if !done {
    fputs(
      "Native WebKit widget integration failed: \(diagnostic), URL=\(widget.url?.absoluteString ?? "none"), loading=\(widget.isLoading)\n",
      stderr)
  }
  return code
}
