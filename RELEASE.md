# WirePulse 4.0.1

Windows: optional priority login startup in the first-launch prompt and Settings → Inicio. Uses a per-user, non-elevated scheduled logon task with no configured delay; Windows still determines actual startup order. Switching modes removes duplicate registrations. Disabling autostart and uninstalling clean up the scheduled task. CPU priority is unchanged. macOS and Linux retain their existing startup mechanisms.

# WirePulse 4.0.0 · Yamato

Yamato integrates the validated Windows Tulipan history with the native macOS app and Linux Mint/Cinnamon X11 in one common release.

- Nine equivalent settings pages, 52 shared controls with checked ranges and defaults, and five music-provider choices including None and Cider. Windows uses WinUI 3; macOS keeps SwiftUI/AppKit; Linux uses Avalonia.
- Drawing resolution from 0.5× to 1.5× is functional on Metal, native Direct3D/Direct2D and Skia. Only the visualization surface is scaled; music cards and text keep native resolution.
- Per-monitor cadence, independent coverage suspension and editor previews. Linux refresh discovery runs asynchronously. macOS no longer pauses the other monitor when editing a display.
- Portable import/export on every platform: relative object positions, layers, styles, locks and embedded image/GIF/video/widget resources in one `.wirepulse` ZIP. Tokens, login settings and graphics API selections stay local.
- GIF timing in the desktop editor uses one monotonic frame source, with 60/75/165/240 FPS regression comparisons.
- Sliders in all desktop editors, one undo action per continuous slider gesture, draggable layers, locks, rotation, duplicate/delete, optional guides and explicit Save/Cancel.
- Optimized front-hemisphere rendering on all platforms, with edges clipped at the silhouette and offscreen patch culling. The original full transparent mesh remains available by disabling optimized rendering.
- Image/GIF/video backgrounds on all platforms: AVFoundation on macOS, Media Foundation on Windows and FFmpeg on Linux. Videos loop silently and stop decoding while suspended. Image/GIF backgrounds use the same centered cover crop on every platform.
- HTML widgets stop live audio updates while suspended; desktop snapshot capture pauses too. Cider tempo is forwarded consistently to widgets.
- Preserves existing scenes, preferences, renderer choices and independent display profiles. Windows still defaults to Direct3D 11 batches for new configurations.

Validation gates include shared-control parity, Windows WinUI editor visibility and resource lifecycle, drawing-resolution changes, GIF delta frames, per-monitor scheduling/coverage, macOS Metal geometry and AVFoundation playback, and real Cinnamon/X11 audio, media metadata, coverage and video tests in CI. Physical dual-monitor 165/75 Hz Windows results come from the earlier user-validated Tulipan build; CI/software drivers do not guarantee a particular physical GPU frame rate.

Native APIs differ by platform: Metal and Windows Direct3D are not interchangeable choices; codec availability depends on the operating system. Linux support targets Cinnamon/X11, not Wayland. macOS packages target Apple silicon and use an ad hoc signature.
