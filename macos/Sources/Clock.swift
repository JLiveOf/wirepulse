import AppKit
import SwiftUI

struct ClockOverlay:View {
 @ObservedObject var config:Configuration
 static func text(_ date:Date,hours24:Bool)->String{let formatter=DateFormatter();formatter.locale=Locale(identifier:"en_US_POSIX");formatter.dateFormat=hours24 ? "HH:mm":"hh:mm a";return formatter.string(from:date)}
 var body:some View{GeometryReader{g in if config.on("clockshow"){TimelineView(.periodic(from:.now,by:1)){context in
  let value=Self.text(context.date,hours24:config.on("clock24")),size=config.n("clocksize")
  let font=config.clockFont=="System" ? NSFont.systemFont(ofSize:size):NSFont(name:config.clockFont,size:size) ?? NSFont.systemFont(ofSize:size)
  let measured=(value as NSString).size(withAttributes:[.font:font])
  let x=max(measured.width/2,min(g.size.width-measured.width/2,g.size.width*config.n("clockX")/100))
  let y=max(measured.height/2,min(g.size.height-measured.height/2,g.size.height*config.n("clockY")/100))
  Text(value).font(Font(font)).foregroundStyle(Color(nsColor:config.color("clockcolor"))).position(x:x,y:y)
 }}}.allowsHitTesting(false)}
}
