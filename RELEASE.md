# WirePulse 4.5.1 · Hotfix

- Detect newer platform builds even when the release version stays the same. Per-platform source identities and their SHA-256 verified manifest prevent repeated offers and downgrades; skipping a build does not hide later hotfixes.
- Closing release highlights with the window close button now stops the music and plays the vintage TV shutdown sound on Windows, macOS and Linux, just like Continue. No music fade-out.
- Splash and welcome/release highlight windows stay above normal application windows on every platform.
- macOS uses the native window frame for its corners instead of an additional fixed glass corner radius.
- Windows retains the borderless splash and native caption controls from the previous hotfix.

The version advances to 4.5.1 so the existing 4.5.0 updater can offer this update. Once installed, WirePulse can also detect future hotfixes with the same version number.
