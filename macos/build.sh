#!/bin/zsh
set -euo pipefail
project_dir=${0:A:h}
app_dir="${WIREPULSE_OUTPUT_DIR:-$project_dir/dist}/WirePulse.app"
mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"
cp "$project_dir/Resources/Info.plist" "$app_dir/Contents/Info.plist"
cp -R "$project_dir/Resources/en.lproj" "$project_dir/Resources/es.lproj" "$app_dir/Contents/Resources/"
cp "$project_dir/../desktop/Assets/en.json" "$app_dir/Contents/Resources/en.json"
cp "$project_dir/../desktop/Assets/Shutdown.wav" "$app_dir/Contents/Resources/Shutdown.wav"
cp "$project_dir/../desktop/Assets/Setup-"*.wav "$app_dir/Contents/Resources/"
cp "$project_dir/../desktop/Assets/whats-new.json" "$app_dir/Contents/Resources/whats-new.json"
cp "$project_dir/../desktop/Assets/Welcome.wav" "$app_dir/Contents/Resources/Welcome.wav"
cp "$project_dir/Resources/WirePulse.icns" "$app_dir/Contents/Resources/WirePulse.icns"
xcrun swiftc "${WIREPULSE_SWIFT_OPT:--O}" -swift-version 5 -target arm64-apple-macos14.2 \
 "$project_dir"/Sources/*.swift \
 -framework AppKit -framework SwiftUI -framework MetalKit -framework CoreAudio -framework Accelerate -framework ServiceManagement -framework WebKit -framework AVFoundation \
 -o "$app_dir/Contents/MacOS/WirePulse"
/usr/bin/codesign --force --sign - --identifier dev.wirepulse.macos "$app_dir"
