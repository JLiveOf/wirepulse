> **Repositorio unificado:** [WirePulse](https://github.com/JLiveOf/wirepulse) · [Descargas para todos los sistemas](https://github.com/JLiveOf/wirepulse/releases/latest)

# WirePulse · Windows & Linux

Native audio-reactive desktop wallpaper. Black background, full-height left spectrum, red wireframe orb and top-centered music card. Shared C# / Avalonia / Skia renderer; platform-native audio and desktop integration. No Electron, browser runtime, microphone or saved audio.

## Platforms

* Windows 10 1809 / Windows 11 x64: WASAPI output loopback, Explorer WorkerW wallpaper, Global System Media Transport Controls.
* Linux Mint / Cinnamon **X11** x64: PulseAudio / PipeWire-Pulse output monitor, EWMH desktop surface with empty input region, MPRIS through playerctl. Cinnamon Wayland is not supported.

Windows and Linux share this source directory. The native Swift / Metal macOS implementation is in [`../macos/`](../macos/). All platform downloads are published together in the root repository releases.

## Build

Install .NET 10 SDK. Windows: `dotnet publish -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -o publish`.
Linux: `dotnet publish -c Release -f net10.0 -r linux-x64 --self-contained true -o publish`.
Run `publish/WirePulse.exe` or `publish/WirePulse`. The self-contained downloads do not need the SDK/runtime installed.

Linux dependencies: `sudo apt install pulseaudio-utils playerctl libx11-6 libxfixes3 libice6 libsm6 libfontconfig1`. PipeWire users need its Pulse compatibility server. Run as your desktop user, never root. Choose the Cinnamon X11 session at login.

## Music & Cider

Automatic mode prefers a playing session, then a paused one. Native media integrations expose only information actually published by the player. Browser service identification depends on the published URL; if unavailable, the browser name is shown. The audio visualizers work independently of metadata.

Cider works on Windows and Linux through system media sessions and its **read-only local playback API**. In Cider enable Connectivity / API, create a Playback token and paste it into WirePulse → Tarjeta. Default address: `http://127.0.0.1:10767`. Current API v2 snapshot is preferred; v1 is retained for compatibility. Requests are GET-only, restricted to loopback, and never follow redirects. Artwork comes from the player's published URL. WirePulse does not read Apple account credentials or request library/account scopes. Settings and token stay in your own application-data folder (0600 on Linux); do not share that folder.

Official API reference: https://taproom.cider.sh/docs/api/ and https://github.com/ciderapp/docs/blob/main/docs/1.client/rpc.md.

## Layout & screens

Open WirePulse from the notification-area icon. Choose matching layouts across all displays or independent display profiles, then choose the display to edit. Each display uses its own logical size, so shared positions are proportional. The default card is top-centered; turn that off to use its horizontal position and bottom margin. Display profiles are saved separately. Identical monitor names are disambiguated by enumeration order.

All original visual groups are available: background, audio sensitivity/bass/noise/smoothing, orb mesh/size/position/deformation/pulse/rotation/density/lines/glow/opacity, spectrum geometry/segments, card/art/progress, frame rate. FFT adapts to output rate and covers DC–Nyquist in 64 contiguous bands. Stereo powers are analyzed independently, so opposite-phase signals remain visible. Higher spectrum bandwidth does not improve or alter lossless playback.

## Validation

Run `dotnet run --project tests/UiTests` to exercise all settings groups, checkboxes and independent display selection without a physical desktop. Run the executable with `--self-test` for DSP, phase, silence, DC/Nyquist and authenticated Cider HTTP fixture tests. GitHub Actions compiles self-contained Windows/Linux packages and runs Linux Cinnamon/Nemo under Xvfb with real PulseAudio monitor capture, generated 1 kHz audio, a fixture MPRIS player, compositor screenshot and X11 state checks. Fixtures contain no copyrighted recordings or private library data. CI is not a replacement for testing actual audio hardware, a signed Cider build or every Explorer/Cinnamon version.

See the workflow logs for the exact status of the current revision. No claim of hardware verification is made by a successful compile.


## Rendimiento desde 2.2

El dibujo sigue el reloj nativo del sistema y puede adaptarse a 165 Hz u otras frecuencias detectadas por pantalla. En Rendimiento elige Automático, Calidad o Ligero; Ligero limita la malla y el resplandor para equipos antiguos. Los FPS de dibujo y el backend GPU/CPU aparecen en Ajustes. Consulta [las mediciones, el método y las limitaciones](../PERFORMANCE.md).
