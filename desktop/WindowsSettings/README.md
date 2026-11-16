# Windows settings

Native WinUI 3 settings for Windows 10 build 19041 and later. The nine categories, symbols, grouped forms and display selection follow the macOS settings window. The editor remains the existing renderer-owned editor, so scenes and saved layouts retain the same behavior and format across platforms.

Build and publish from `desktop` with `./build-windows.ps1`. This includes the .NET and Windows App SDK runtimes and the generated `WirePulse.Settings.pri` index. Publishing only `WirePulse.csproj` intentionally retains the existing fallback settings window for development builds.

The settings process communicates through a random, current-user-only named pipe. The renderer owns configuration objects, validation, asset imports and disk writes. Slider updates are coalesced at 60 ms; changing pages or polling live audio does not rebuild the wallpaper. Closing settings stops polling and releases the WinUI process without stopping the wallpaper.

`./build-windows.ps1 -Test` exercises all native pages, applies a real native slider through the pipe, verifies display isolation, restores the original setting and tests capture restart. Audio hardware checks are reported as skipped when a runner has no WASAPI endpoint. `tests/windows-displays.ps1` also checks real maximized/minimized windows on each attached display.
