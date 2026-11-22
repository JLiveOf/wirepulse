import AppKit

final class AnimatedTextView:NSView {
 override var isFlipped:Bool{true}
 private var item=SceneItem(),elapsed=0.0,last=0.0,timer:Timer?,paused=false
 private var cachedKey="",cachedString:NSAttributedString?
 func update(_ value:SceneItem){if value != item{item=value;cachedKey="";needsDisplay=true};sync()}
 func pause(_ value:Bool){paused=value;sync()}
 override func viewDidMoveToWindow(){super.viewDidMoveToWindow();sync()}
 private func sync(){let run=window != nil && !paused && item.textAnimation != "none" && !isHidden;if run && timer==nil{last=ProcessInfo.processInfo.systemUptime;timer=Timer.scheduledTimer(withTimeInterval:1/60,repeats:true){[weak self] _ in guard let self=self else{return};let now=ProcessInfo.processInfo.systemUptime;self.elapsed+=max(0,now-self.last);self.last=now;self.needsDisplay=true}}else if !run{timer?.invalidate();timer=nil;last=0}}
 deinit{timer?.invalidate()}
 override func draw(_ dirtyRect:NSRect){
  let motion=TextMotion.frame(item.textAnimation,elapsed,item.textSpeed,item.textDistance,item.text.count)
  let value=String(item.text.prefix(motion.characters)),key="\(bounds.size)|\(value)|\(item.font)|\(item.textSize)|\(item.textBold)|\(item.textItalic)|\(item.textAlign)|\(item.color)|\(item.gradients["color"] ?? GradientSpec())"
  if key != cachedKey{cachedKey=key;let size=item.textSize>0 ? item.textSize:max(8,bounds.height*0.65/Double(max(1,item.text.components(separatedBy:"\n").count)));var font=item.font=="System" ? NSFont.systemFont(ofSize:size):(NSFont(name:item.font,size:size) ?? .systemFont(ofSize:size));if item.textBold{font=NSFontManager.shared.convert(font,toHaveTrait:.boldFontMask)};if item.textItalic{font=NSFontManager.shared.convert(font,toHaveTrait:.italicFontMask)};let paragraph=NSMutableParagraphStyle();paragraph.alignment=item.textAlign=="left" ? .left:item.textAlign=="right" ? .right:.center
   var color=NSColor(hex:item.color);if let gradient=item.gradients["color"],gradient.enabled,bounds.width>0,bounds.height>0{let image=NSImage(size:bounds.size,flipped:true){rect in let(a,b)=gradient.points(rect.width,rect.height);NSGradient(starting:NSColor(hex:self.item.color),ending:NSColor(hex:gradient.end))?.draw(from:a,to:b,options:[.drawsBeforeStartingLocation,.drawsAfterEndingLocation]);return true};color=NSColor(patternImage:image)}
   cachedString=NSAttributedString(string:value,attributes:[.font:font,.foregroundColor:color,.paragraphStyle:paragraph])
  }
  guard let text=cachedString,let cg=NSGraphicsContext.current?.cgContext else{return};let measured=text.boundingRect(with:NSSize(width:bounds.width,height:100000),options:[.usesLineFragmentOrigin,.usesFontLeading]);cg.saveGState();cg.setAlpha(motion.opacity);cg.translateBy(x:bounds.midX+motion.x,y:bounds.midY+motion.y);cg.scaleBy(x:motion.scale,y:motion.scale);cg.translateBy(x:-bounds.midX,y:-bounds.midY);text.draw(with:CGRect(x:0,y:max(0,(bounds.height-measured.height)/2),width:bounds.width,height:max(bounds.height,measured.height)),options:[.usesLineFragmentOrigin,.usesFontLeading]);cg.restoreGState()
 }
}
