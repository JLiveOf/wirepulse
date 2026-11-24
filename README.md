# WirePulse, Make your wallpaper groovy

[Español](README.es.md)

An audio-reactive wallpaper for **Windows, macOS and Linux Mint / Cinnamon X11**.

- Move, resize, rotate and layer objects in the desktop editor.
- Add orbs, visualizers, clocks, animated text, images, GIFs and HTML widgets.
- Customize colors and gradients; import and export layouts across systems.
- Show music metadata and artwork from supported players, including Cider.
- Use shared or independent layouts on multiple displays.
- Check GitHub for updates, with stable and beta channels.

## Download

**[Get the latest release](https://github.com/JLiveOf/wirepulse/releases/latest)**

| System | Package |
| --- | --- |
| Windows 10/11 x64 | `win-x64-Setup.exe` or portable `.zip` |
| macOS 14.2+, Apple silicon | `macOS-arm64.zip` — move the app to Applications |
| Linux Mint / Cinnamon **X11**, x64 | `.deb` or portable `linux-x64.tar.gz` |

Linux uses native GTK 3 controls; the `.deb` installs their dependencies. Portable users also need `python3-gi`, `python3-gi-cairo`, `gir1.2-gtk-3.0` and `librsvg2-common`. Linux video backgrounds require FFmpeg. Audio and artwork may require system permissions. macOS builds are ad-hoc signed, not notarized; Windows builds are unsigned.

## Edit your desktop

Open WirePulse from the tray or menu bar → **Edit desktop**. Add objects, drag them into place and save. Text formatting, animation and gradients are in each object's properties.

https://github.com/user-attachments/assets/586ff496-34da-49b2-820b-7a625c7f0e12

Settings → **Startup** contains language, login startup and update preferences. Language changes require a restart. Beta includes GitHub prereleases. Updates ask before downloading; skipping one version or disabling future prompts is remembered. Linux `.deb` updates open the system package installer.

## Build

[Windows / Linux](desktop/README.md) · [macOS](macos/README.md) · [Report a bug](https://github.com/JLiveOf/wirepulse/issues)
