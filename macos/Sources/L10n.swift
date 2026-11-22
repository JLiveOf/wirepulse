import Foundation

enum L10n {
 static let preferenceURL=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("WirePulse/language.txt")
 static var preference:String{(try? String(contentsOf:preferenceURL,encoding:.utf8).trimmingCharacters(in:.whitespacesAndNewlines)).flatMap{["es","en"].contains($0) ? $0:nil} ?? "system"}
 static func setPreference(_ value:String){try? FileManager.default.createDirectory(at:preferenceURL.deletingLastPathComponent(),withIntermediateDirectories:true);try? (["es","en"].contains(value) ? value:"system").write(to:preferenceURL,atomically:true,encoding:.utf8)}
 static func resolve(_ requested:String,_ system:String)->String{["es","en"].contains(requested) ? requested:(system.lowercased().hasPrefix("es") ? "es":"en")}
 static let language=resolve(ProcessInfo.processInfo.environment["WIREPULSE_LANGUAGE"] ?? preference,Locale.preferredLanguages.first ?? "en")
 static let english:[String:String]={guard let url=Bundle.main.url(forResource:"en",withExtension:"json"),let data=try? Data(contentsOf:url),let result=try? JSONDecoder().decode([String:String].self,from:data) else{return [:]};return result}()
 static let pattern: NSRegularExpression? = {let keys=english.keys.sorted{$0.count>$1.count}.map{ key in (key.first?.isLetter==true ? "(?<![\\p{L}])":"")+NSRegularExpression.escapedPattern(for:key)+(key.last?.isLetter==true ? "(?![\\p{L}])":"")};return try? NSRegularExpression(pattern:keys.joined(separator:"|"))}()
 static func error(_ error:Error)->NSError{let original=error as NSError;var info=original.userInfo;info[NSLocalizedDescriptionKey]=text(original.localizedDescription);if let reason=original.localizedFailureReason{info[NSLocalizedFailureReasonErrorKey]=text(reason)};if let suggestion=original.localizedRecoverySuggestion{info[NSLocalizedRecoverySuggestionErrorKey]=text(suggestion)};return NSError(domain:original.domain,code:original.code,userInfo:info)}
 static func text(_ value:String)->String{translate(value,language)}
 static func translate(_ value:String,_ language:String)->String{guard language=="en",!value.isEmpty else{return value};if let exact=english[value]{return exact};guard let regex=pattern else{return value};let original=value as NSString;var result=value;for match in regex.matches(in:value,range:NSRange(location:0,length:original.length)).reversed(){if let range=Range(match.range,in:result),let replacement=english[original.substring(with:match.range)]{result.replaceSubrange(range,with:replacement)}};return result}
}
