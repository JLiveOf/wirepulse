import AppKit
import AVFoundation

/// Retains image/GIF decoding and loops video through AVFoundation; no per-frame file reads.
final class BackgroundMedia:NSView {
 private let image=NSImageView(),video=AVPlayerLayer()
 private var queue:AVQueuePlayer?,looper:AVPlayerLooper?,path="",paused=false
 override init(frame:NSRect){super.init(frame:frame);wantsLayer=true;layer?.masksToBounds=true;image.imageScaling = .scaleProportionallyUpOrDown;image.animates=true;image.autoresizingMask=[];clipsToBounds=true;image.frame=bounds;addSubview(image);video.videoGravity = .resizeAspectFill;layer?.addSublayer(video)}
 required init?(coder:NSCoder){fatalError()}
 var playbackSeconds:Double{let time=queue?.currentTime().seconds ?? 0;return time.isFinite ? time:0}
 var hasVideo:Bool{queue != nil}
 var animatesImage:Bool{image.animates}
 static func cover(_ size:CGSize,in bounds:CGRect)->CGRect{guard size.width>0,size.height>0 else{return bounds};let scale=max(bounds.width/size.width,bounds.height/size.height),w=size.width*scale,h=size.height*scale;return CGRect(x:bounds.midX-w/2,y:bounds.midY-h/2,width:w,height:h)}
 override func layout(){super.layout();video.frame=bounds;image.frame=Self.cover(image.image?.size ?? bounds.size,in:bounds)}
 func load(_ next:String){guard next != path else{return};path=next;queue?.pause();looper?.disableLooping();looper=nil;queue?.removeAllItems();queue=nil;video.player=nil;image.image=nil
  guard !next.isEmpty else{return}
  if ["mp4","m4v","mov","webm","avi","mkv"].contains(URL(fileURLWithPath:next).pathExtension.lowercased()){let player=AVQueuePlayer();player.isMuted=true;let item=AVPlayerItem(url:URL(fileURLWithPath:next));looper=AVPlayerLooper(player:player,templateItem:item);queue=player;video.player=player;if !paused{player.play()}}
  else{image.image=NSImage(contentsOfFile:next);image.animates = !paused;needsLayout=true;layoutSubtreeIfNeeded()}
 }
 func suspend(_ value:Bool){paused=value;image.animates = !value;if value{queue?.pause()}else{queue?.play()}}
 deinit{queue?.pause();looper?.disableLooping()}
}
