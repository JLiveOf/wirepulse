import Foundation
import Security

/// Read-only local Cider playback API. Tokens never follow redirects or leave loopback.
final class CiderReader:NSObject,URLSessionTaskDelegate {
 private static let key="dev.wirepulse.macos.cider"
 static func loadToken()->String{var result:CFTypeRef?;let query:[String:Any]=[kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:key,kSecAttrAccount as String:"local-api",kSecReturnData as String:true,kSecMatchLimit as String:kSecMatchLimitOne];guard SecItemCopyMatching(query as CFDictionary,&result)==errSecSuccess,let data=result as? Data else{return ""};return String(data:data,encoding:.utf8) ?? ""}
 static func saveToken(_ token:String){let query:[String:Any]=[kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:key,kSecAttrAccount as String:"local-api"];if token.isEmpty{SecItemDelete(query as CFDictionary);return};let data=Data(token.utf8);let result=SecItemUpdate(query as CFDictionary,[kSecValueData as String:data] as CFDictionary);if result==errSecItemNotFound{var q=query;q[kSecValueData as String]=data;q[kSecAttrAccessible as String]=kSecAttrAccessibleAfterFirstUnlock;SecItemAdd(q as CFDictionary,nil)}}
 func urlSession(_ session:URLSession,task:URLSessionTask,willPerformHTTPRedirection response:HTTPURLResponse,newRequest request:URLRequest,completionHandler:@escaping(URLRequest?)->Void){completionHandler(nil)}
 static func tempo(_ info:[String:Any])->Double{let nodes=[info,info["attributes"] as? [String:Any] ?? [:],info["audioFeatures"] as? [String:Any] ?? [:],info["audioAnalysis"] as? [String:Any] ?? [:]];for node in nodes{for key in ["bpm","tempo"]{let value:Double;if let number=node[key] as? NSNumber,CFGetTypeID(number) != CFBooleanGetTypeID(){value=number.doubleValue}else if let text=node[key] as? String,let number=Double(text){value=number}else{continue};if value.isFinite && value>=20 && value<=400{return value}}};return 0}
 static func read(address:String,token:String)->[String:Any]?{
  guard let host=URL(string:address),host.scheme=="http",["127.0.0.1","localhost","[::1]","::1"].contains(host.host ?? "") else{return nil}
  let delegate=CiderReader(),session=URLSession(configuration:.ephemeral,delegate:delegate,delegateQueue:nil);defer{session.invalidateAndCancel()}
  func get(_ path:String)->[String:Any]?{guard let url=URL(string:path,relativeTo:host) else{return nil};var request=URLRequest(url:url);request.timeoutInterval=1;request.setValue(token,forHTTPHeaderField:"apptoken");let sem=DispatchSemaphore(value:0);var value:[String:Any]?;session.dataTask(with:request){bytes,response,_ in defer{sem.signal()};if let response=response as? HTTPURLResponse,response.statusCode==200,let bytes=bytes{value=(try? JSONSerialization.jsonObject(with:bytes)) as? [String:Any]}}.resume();guard sem.wait(timeout:.now()+1.3) == .success else{return nil};return value}
  let snapshot=get("/api/v2/playback")?["data"] as? [String:Any]
  guard let info=(snapshot?["nowPlaying"] as? [String:Any]) ?? (get("/api/v1/playback/now-playing")?["info"] as? [String:Any]),let name=info["name"] as? String,!name.isEmpty else{return nil}
  let time=snapshot?["time"] as? [String:Any]
  let playing=snapshot == nil ? (get("/api/v1/playback/is-playing")?["is_playing"] as? Bool ?? false):snapshot?["state"] as? String=="playing"
  let artwork=(info["artwork"] as? [String:Any])?["url"] as? String ?? ""
  return ["bpm":tempo(info),"provider":"Cider","kind":"player","title":name,"artist":info["artistName"] as? String ?? "","album":info["albumName"] as? String ?? "","duration":time?["duration"] as? Double ?? (info["durationInMillis"] as? Double ?? 0)/1000,"position":time?["currentTime"] as? Double ?? info["currentPlaybackTime"] as? Double ?? 0,"playing":playing,"art":artwork.replacingOccurrences(of:"{w}",with:"600").replacingOccurrences(of:"{h}",with:"600"),"id":(info["playParams"] as? [String:Any])?["id"] as? String ?? name]
 }
}
