# Desktop editor

Open **Editar escritorio…** from Settings or the tray/menu-bar menu. On macOS, opening the app again shows Settings; Command+E opens the editor while WirePulse is active. In independent-display mode, Settings edits the selected display; the tray opens the primary/first display. In shared mode the saved normalized composition is applied to every display.

Select an object on the canvas or in the layer list. Drag its body to move it, or use an edge/corner handle to resize. Unlock background layers in the list to move them. **Mantener proporciones** controls aspect ratio; hold Alt/Option to temporarily resize freely or bypass snapping. **Ajustar a guías** enables screen and other-object alignment within eight logical pixels. Objects stay partly reachable when dragged beyond the screen.

Use **Añadir** for built-ins, text, images/GIFs, and HTML widgets. Copies have independent style and geometry. The inspector controls visibility, lock, fonts, colors, style, ordering and opacity. Changes are a draft until **Guardar diseño**; **Cancelar** discards them. Undo/redo and duplicate are also available. Existing layouts are migrated when opening the editor and only persisted on Save. Reset restores the built-in layout.

Images are copied into WirePulse's application-support asset library, so moving the original file does not break the saved desktop. GIFs animate and loop; resizing does not rewrite the source. Imported assets can be up to 128 MiB and HTML packages up to 2048 entries. The desktop is click-through outside edit mode; HTML widgets are visual, not interactive desktop applications.

## HTML widgets

Import a self-contained HTML file, a folder with `index.html`, or a ZIP containing that entry point and its resources. Prefer relative asset URLs. HTML/CSS/JavaScript animations run inside the system browser engine without a native bridge; do not import untrusted content. Widgets can request network resources through the browser, but receive no host file/process API, microphone/camera permission, or download access. Binary plugins and unrelated widget formats are not supported.

Listen for the read-only event:

```js
window.addEventListener('wirepulse:audio', ({ detail }) => {
  // detail: { bands: number[64], beat: number, bpm: number, playing: boolean }
  // Also available as window.wirepulseAudio.
});
```

BPM may be zero while the detector is settling; it is an estimate from captured audio. The animated background still uses the existing optional Cider tempo when available. See `examples/beat-widget/index.html`.

Windows requires the Microsoft Edge WebView2 Evergreen Runtime for HTML widgets (already present on most supported Windows installations). Linux Mint requires `libwebkit2gtk-4.1-0`; the DEB declares it. macOS includes WebKit. Windows/Linux widgets use snapshots at up to 10 FPS and at most 1920×1080; other visualizers render separately at their selected refresh rate. Large GIFs, multiple mesh layers or complex web animations can increase GPU/CPU usage; reduce density and layer count on older computers.

### Object locks, angle and orb projection

Use the padlock next to each layer to decide whether it can move. New/migrated meshes are unlocked; previously saved lock choices are preserved. Unlock an object before deleting it with **Eliminar objeto**. Delete/Backspace while typing edits the text instead of deleting the object.

**Ángulo (°)** rotates every type of object, including images/GIFs, text, cards and widgets. Resize handles and hit testing follow the rotated object. ±90° and **Sin giro** are shortcuts.

An orb's **Render optimizado** option conservatively rejects complete mesh patches outside the viewport before projecting their vertices. It preserves all rear wire that remains visible through the front mesh. It does not change density, approximate the sphere with an image or flatten its audio deformation. Disable the option to retain the earlier full projection. The gain depends on how much of the orb is offscreen; a fully visible orb avoids the patch-culling overhead.

On Windows, **Rendimiento → Motor de visualizadores** changes actual line drawing between Skia, batched Direct3D 11, and Direct2D paths. Native engines require compatible shared D3D textures; otherwise Skia handles drawing. These choices are separate from the interface API. Native engines use the DirectX 11 interface; choose Skia to retain Vulkan/OpenGL/CPU selection. The status report identifies active engines and fallback messages. Native Windows drawing does not replace the native Metal implementation on macOS.

Windows backgrounds accept images, animated GIFs and muted looping video through **Elegir archivo…**. Files are copied into managed app storage, so moving the original does not break the background. Videos have a 2 GB import limit; codec availability follows Windows Media Foundation. Background videos are not audio sources for beat detection. No video encoder is used.

Implementation references: [Avalonia GPU interop](https://github.com/AvaloniaUI/Avalonia/tree/master/samples/GpuInterop), [Direct2D/Direct3D interoperation](https://learn.microsoft.com/en-us/windows/win32/direct2d/direct2d-and-direct3d-interoperation-overview), [Vortice Windows bindings](https://github.com/amerkoleci/Vortice.Windows), [Media Engine sample](https://github.com/amerkoleci/Vortice.Windows.Samples/tree/main/src/MediaFoundation/MediaEnginePlayVideo).
