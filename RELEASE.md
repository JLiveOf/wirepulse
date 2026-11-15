WirePulse 3.0 introduces a desktop composition editor on Windows, Linux Mint/Cinnamon X11, and macOS.

- Open **Editar escritorio** from Settings or the tray/menu-bar icon. Drag objects and resize with handles; optionally snap to screen/object edges and centers. Save explicitly, cancel, undo/redo, duplicate, lock, hide and reorder layers.
- Add any number of built-in orbs, spectra, music cards, clocks, stars and meshes, plus custom text, images, animated GIFs and HTML widgets. Images/GIFs can preserve their aspect ratio or resize freely.
- Windows/Linux Settings now have a sidebar and visual color chooser. Position sliders are replaced with the editor. Existing layouts remain unchanged until an edited layout is saved; shared and independent monitor profiles are preserved.
- HTML/CSS/JavaScript widgets import from HTML, a folder containing index.html, or ZIP. They receive read-only spectrum/beat/BPM events. A sample is included in examples/beat-widget. This format does not run Rainmeter skins or Wallpaper Engine binary plugins.
- Windows HTML widgets need Microsoft Edge WebView2 Runtime. Linux packages depend on WebKitGTK 4.1; macOS uses native WebKit. Windows/Linux widget snapshots update at 10 FPS to keep browser work bounded; ordinary visualizers keep their configured native display rate.

Downloads: Windows x64 installer or portable ZIP, Linux x64 DEB or portable tar.gz, macOS Apple silicon app ZIP. Source is shared in this repository. Previous renderer fallback, system-audio capture, Cider metadata and startup consent remain available.
