#!/bin/zsh
set -euo pipefail
project_dir=${0:A:h}
app_dir="${WIREPULSE_OUTPUT_DIR:-$project_dir/dist}/WirePulse.app"
mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"
cp "$project_dir/Resources/Info.plist" "$app_dir/Contents/Info.plist"
cp "$project_dir/Resources/WirePulse.icns" "$app_dir/Contents/Resources/WirePulse.icns"
xcrun swiftc -O -swift-version 5 -target arm64-apple-macos14.2 \
 "$project_dir"/Sources/*.swift \
 -framework AppKit -framework SwiftUI -framework MetalKit -framework CoreAudio -framework Accelerate \
 -o "$app_dir/Contents/MacOS/WirePulse"
/usr/bin/codesign --force --sign - --identifier dev.wirepulse.macos "$app_dir"
