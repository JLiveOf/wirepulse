# Windows 3.2.0: native settings and reliability

Windows now has a native WinUI 3 settings window with the same nine categories, corresponding vector symbols, grouped controls and per-display selection as macOS. Existing scenes, object editing and configuration formats remain compatible. macOS and Linux source behavior is preserved; the Windows settings executable and runtime are included only in Windows packages.

## Fixes

- Correctly convert Win32 monitor work-area RECT coordinates to dimensions. Maximized-window suspension now works on secondary monitors, including monitors with a nonzero origin.
- Ignore transparent, nonactivating and tool windows when deciding whether another application covers a display.
- Treat Media Foundation S_FALSE as “no new frame,” avoiding repeated video transfers at the interface refresh rate. Pausing also stops transfers; resizing can refresh the retained frame.
- Wait for an in-flight GPU frame before releasing native resources. Clean up partially allocated texture pairs during fallback.
- Keep each WASAPI callback bound to its own endpoint format, reconnect when capture stops, support 8-bit PCM and initialize FFT diagnostics during silence. Repeated Windows capture disposal is safe.
- Coalesce native settings sliders and flush pending changes when closing. The renderer owns configuration validation, managed asset imports and writes.
- Isolate Windows test-instance locks. Desktop tests no longer reveal hidden IME/helper windows, and restore visible windows they temporarily minimize.

## Validation on the Windows PC

RTX 3050, 1920×1080 LG ULTRAGEAR and 1440×900 Acer AL1916W. Both native settings profiles were exercised independently. Real maximized/minimized windows and transparent overlays were checked on each display.

Native WinUI pages, slider-to-renderer updates, capture stop/resume, real low-volume 1 kHz WASAPI loopback, GIF pixel equivalence, orb equivalence, native GPU teardown, Direct3D/Direct2D, graphics fallbacks, video/GIF backgrounds and live edits were tested. The real tone was detected at 1001.95 Hz with 48 kHz capture and an 8192-point FFT.

In a seven-second video fixture, transfers decreased from 307 before the change to roughly 136–152 afterward, depending on scheduling. During the paused interval the transfer count stays constant. This measures transfers, not overall GPU utilization or sustained desktop FPS. The current Windows session exposes 60 Hz and 75 Hz; this is not a claim of validation at 165 Hz.

Use `./build-windows.ps1 -Test` and `./tests/windows-displays.ps1` to reproduce the Windows checks. Hardware-less CI runners explicitly skip WASAPI hardware checks. Build output remains version 3.2.0 to preserve the repository's cross-platform version scheme; no macOS or Linux release is republished by this change alone.
