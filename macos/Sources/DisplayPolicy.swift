import AppKit

/// Geometry is compared in Quartz's top-left coordinate space, independently per display.
enum DisplayPolicy {
 static func covers(_ outer:CGRect,_ inner:CGRect,tolerance:CGFloat=2)->Bool{outer.minX<=inner.minX+tolerance && outer.minY<=inner.minY+tolerance && outer.maxX>=inner.maxX-tolerance && outer.maxY>=inner.maxY-tolerance}
 static func quartz(_ rect:CGRect,desktopTop:CGFloat)->CGRect{CGRect(x:rect.minX,y:desktopTop-rect.maxY,width:rect.width,height:rect.height)}
 static func reason(screen:NSScreen,config:Configuration,windows:[[String:Any]])->String{
  let top=NSScreen.screens.first?.frame.maxY ?? 0,full=quartz(screen.frame,desktopTop:top),work=quartz(screen.visibleFrame,desktopTop:top)
  for window in windows{
   guard (window[kCGWindowOwnerPID as String] as? Int) != Int(ProcessInfo.processInfo.processIdentifier),window[kCGWindowLayer as String] as? Int==0,(window[kCGWindowAlpha as String] as? Double ?? 1)>0,let raw=window[kCGWindowBounds as String] as? [String:Any],let rect=CGRect(dictionaryRepresentation:raw as CFDictionary) else{continue}
   if covers(rect,full){if config.on("pausefullscreen"){return "pantalla completa"}}
   else if config.on("pausemaximized") && covers(rect,work){return "ventana maximizada"}
  }
  return ""
 }
}
