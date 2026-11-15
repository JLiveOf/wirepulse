WirePulse 3.1 adds native Windows visualization engines and more control over desktop objects.

- **Windows → Settings → Performance → Visualization engine**: choose Skia, Direct3D 11 GPU batches, or Direct2D vector drawing. Native engines share GPU textures with the interface; Skia remains available and is the fallback when sharing is unsupported. The existing Vulkan, DirectX 11, OpenGL and CPU interface choices remain under Skia. Performance varies with hardware and drivers.
- **Orb properties → Render optimizado** skips mesh patches that cannot contribute to the screen, while retaining visible rear wires, deformation, glow and draw order. Disable it to use the previous projection. Tests compare old/new pixels on Windows/Linux and visible triangles on Metal.
- Every object has a visible padlock in the layer list. The default mesh is no longer automatically locked when migrating a layout; saved user locks remain respected. A toolbar **Eliminar objeto** button makes deletion discoverable.
- Rotate any object by a custom angle, ±90°, or reset. Selection outlines, hit testing and resize handles follow the rotation.
- The Windows background setting now opens a file picker. Static images and animated GIFs use a retained media layer. Videos loop silently using native Media Foundation/Direct3D decoding, with a readback compatibility path for software composition. MP4/H.264 is recommended; other video formats depend on Windows codecs. Imported media is copied into app storage asynchronously.

Downloads below include the Windows x64 installer/portable ZIP, Linux Mint/Cinnamon X11 DEB/portable tar.gz, and macOS Apple silicon app ZIP. This update does not require changing existing layouts.
