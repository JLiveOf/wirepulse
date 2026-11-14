Native WirePulse for Windows or Linux Mint / Cinnamon X11, with the same shared renderer and settings.

- Black background, full-height left LED spectrum, reactive red wireframe orb, top-centered music card.
- WASAPI loopback on Windows; PulseAudio/PipeWire output monitor on Linux. Full DC–Nyquist adaptive FFT, stereo phase-safe analysis.
- Native media sessions plus Cider local API v2, with v1 compatibility. Cider tokens need Playback scope only.
- Shared positions across monitors or separately saved layouts per monitor.
- Self-contained x64 app; no .NET runtime install required.

Windows: extract the ZIP completely, then open WirePulse.exe. Linux Mint: install the .deb through the package installer, or extract the tar.gz and run WirePulse. Choose Cinnamon X11 at login; Wayland is unsupported. Settings open from the notification-area icon. Cider setup is in Settings → Tarjeta.

Validation includes native Windows/Linux builds, DSP and Cider protocol fixtures, saved display profiles, and a Cinnamon/Nemo X11 session with live PulseAudio monitor capture and a generated MPRIS player. Actual Windows audio hardware, physical multi-monitor arrangements and an installed Cider client have not been exercised on the development Mac. Please report system-specific issues with the OS and desktop version. Windows binaries are unsigned.

The macOS native app remains in [wirepulse-macos](https://github.com/JLiveOf/wirepulse-macos).
