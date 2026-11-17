# Yamato 4.0.0 · cross-platform parity

The Windows stable history through `e61e19f` is included, rather than replaced. Swift/AppKit/Metal remains native on macOS. All download formats share VERSION and are published only after the platform jobs pass.

| Capability | Windows | macOS | Linux Mint / Cinnamon X11 |
| --- | --- | --- | --- |
| Nine settings categories, same 52 controls/ranges/defaults | WinUI 3 | SwiftUI | Avalonia |
| Drawing resolution 0.5–1.5×, separate sharp text/card layers | D3D11 / Direct2D / Skia | Metal | Skia |
| Automatic / Quality / Light performance profiles | Yes | Yes | Yes |
| Automatic / None / Apple Music / Spotify / Cider providers | System sessions + Cider API | Apple Events + Cider API | MPRIS + Cider API |
| System output audio, 64 FFT bands, beat/BPM | WASAPI loopback | Core Audio process tap | PulseAudio/PipeWire monitor |
| Capture start/stop | Yes | Yes | Yes |
| Independent or shared monitor layouts | Yes | Yes | Yes |
| Independent cadence; editor pauses covered monitor only | Yes | Yes | Yes |
| Fullscreen / maximized suspension preferences | Win32 state + geometry | Quartz visible window geometry | EWMH state + geometry |
| Static / GIF / silent looping video background | Media Foundation | AVFoundation / NSImageView | FFmpeg / Skia |
| Orb, spectrum, card, clock, stars, mesh, image/GIF, text, HTML widget objects | Yes | Yes | Yes |
| Drag/resize/rotate, lock, delete, duplicate, center, fit | Yes | Yes | Yes |
| Drag layers, optional snapping, undo/redo, isolated draft, Save/Cancel | Yes | Yes | Yes |
| Style sliders; one undo step per pointer gesture | Yes | Yes | Yes |
| Font/color selection and object opacity | Yes | Yes | Yes |
| Optimized front clipping + offscreen patch culling; original mesh toggle | Yes | Yes | Yes |
| Per-user launch consent and startup toggle | Registry Run | ServiceManagement | XDG autostart |

`scripts/check-parity.py` checks shared controls against the native Swift descriptors, slider limits/steps, numeric defaults, categories and provider choices. Headless editor tests and native platform tests cover behavior separately; source-level parity checks alone do not prove a button works.

## Platform boundaries

- Graphics API selectors are platform-specific: Direct3D/Direct2D on Windows, Metal on macOS and the available Skia backend on Linux. DirectX 12 and VRR are not implemented.
- Apple Music on Linux is consumed through Cider/MPRIS/local Cider API; this does not install an Apple Music desktop client. Provider artwork/metadata depends on what the provider exposes. Animated Apple Music artwork is not supplied by the existing interfaces.
- Codec availability differs. MP4/H.264 is the portable baseline. FFmpeg is a Linux dependency, included in DEB dependencies. Linux video decoding is bounded to 1920×1080/30 FPS, independent of visualization refresh, to avoid full-resolution raw-frame traffic on older PCs.
- macOS coverage uses visible normal-window geometry without Accessibility window-state permission; it identifies maximized coverage rather than a private app-specific maximize flag. Linux ignores other workspaces, hidden/unmapped and override-redirect windows.
- CI exercises Cinnamon/X11, native Windows/software GPU drivers and Apple-silicon macOS. It does not emulate physical dual 165/75 Hz monitors or guarantee hardware FPS. Manual checks must be reported separately.
- Preferences are not reset or overwritten during update. Per-monitor and shared preferences keep their existing storage paths. Backups are taken before installing the macOS app.
