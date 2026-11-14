import AppKit

// Runs on the helper process's main thread. AEDesc.data preserves the image bytes;
// osascript's human-readable output is not a reliable image transport.
func exportMusicArtwork(expectedID:String)->Int32 {
 let validID = !expectedID.isEmpty && expectedID.range(of:"^[0-9a-fA-F]+$",options:.regularExpression) != nil
 let check = validID ? "if persistent ID of current track is not \"\(expectedID)\" then return missing value":""
 var code=0
 for property in ["raw data","data"]{
  let source="""
  tell application id "com.apple.Music"
   if not running then return missing value
   \(check)
   return \(property) of artwork 1 of current track
  end tell
  """
  guard let script=NSAppleScript(source:source) else{continue}
  var error:NSDictionary?;let descriptor=script.executeAndReturnError(&error)
  if let error=error{code=error[NSAppleScript.errorNumber] as? Int ?? -1;continue}
  let bytes=descriptor.data
  if let image=NSImage(data:bytes),image.isValid{FileHandle.standardOutput.write(bytes);return 0}
 }
 fputs("Apple Music no ha proporcionado una imagen válida (código \(code)).",stderr);return 1
}
