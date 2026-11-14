import Foundation
import Darwin

// Bundle.main caches its launch path. The kernel resolves the running vnode's
// current path, including when Finder moves the bundle while the app is open.
func currentExecutableURL()->URL? {
 var path=[CChar](repeating:0,count:4096)
 let length=proc_pidpath(getpid(),&path,UInt32(path.count))
 if length>0 {
  let url=URL(fileURLWithPath:String(cString:path))
  if FileManager.default.isExecutableFile(atPath:url.path){return url}
 }
 if let url=Bundle.main.executableURL,FileManager.default.isExecutableFile(atPath:url.path){return url}
 return nil
}
