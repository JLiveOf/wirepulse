WirePulse 3.2 focuses on live-edit stability, animation decoding and desktop-editor responsiveness.

- **Drag layers to any position** using the grip in the object list. A drop indicator shows insertion before/after a row; the top row is the front layer. Selection, undo/redo and explicit Save/Cancel preserve the order. Available on Windows, Linux and macOS.
- Live color, opacity, density, font and other property edits retain existing objects and visualizers instead of destroying/recreating them. This preserves animation phase and avoids blank frames, repeated image loads and GPU-device recreation.
- Windows/Linux GIF playback uses sequential delta-frame decoding, immutable reusable frame images and a bounded cache (64 MiB per animation, 128 MiB total). Animated decoding runs off the UI thread with at most two workers. Rendering keeps the last complete frame when decoding is late; frames are invalidated only when they change.
- Hidden/offscreen images stop advancing. Unchanged layer transforms and native view order no longer get reset on every property edit.
- macOS no longer blocks the editor waiting for an available Metal command buffer. The underlying wallpapers pause while the desktop editor is open and resume after Save/Cancel. Text font changes update the retained text view; font inventories are cached.

Validation includes delta GIFs across multiple loops, transparency/disposal regressions, retained-renderer identity during repeated live edits, arbitrary layer order and undo, native Windows desktop presentation and package installation, and macOS editor checks. Performance measurements are workload-specific, not a guarantee of a particular refresh rate on untested hardware.

Downloads: Windows x64 installer/portable ZIP, Linux Mint/Cinnamon X11 DEB/portable tar.gz, macOS Apple silicon ZIP.
