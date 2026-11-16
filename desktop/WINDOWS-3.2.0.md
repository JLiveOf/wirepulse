# Tulipan · Windows beta 2

La segunda beta se centra en la orientación del motor nativo. Las texturas Direct3D 11 usan origen superior izquierdo, pero su importación omitía TopLeftOrigin y las presentaba invertidas. Ahora se declara explícitamente para los motores nativos y el vídeo. Una prueba asimétrica exige rojo arriba a la izquierda y verde abajo a la derecha en la imagen final compuesta; se ha comprobado que falla con el código anterior y pasa con la corrección.

Direct3D 11 por lotes es el valor predeterminado de Windows para ajustes nuevos o antiguos que no declaren motor. Las elecciones de motor que ya estuvieran guardadas se respetan. Skia sigue siendo la alternativa de compatibilidad si no se pueden compartir texturas.

Las pruebas del usuario con su pantalla a 165 Hz señalan una mejora importante al pasar de Skia a Direct3D por lotes. Las mediciones iniciales de 60/75 Hz que aparecen abajo corresponden a una sesión anterior y no deben confundirse con la configuración actual. La migración completa del inspector del editor a WinUI y la ocultación de geometría posterior quedan pendientes para la siguiente iteración; no forman parte de esta corrección enfocada.

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

## Tulipan estable · Windows

El inspector de objetos y la barra inferior son ventanas WinUI 3 nativas, vinculadas al borrador del editor. Los rangos usan Slider; colores, interruptores, capas y diálogos usan controles WinUI. Guardar confirma el borrador; cancelar lo descarta; los gestos de deslizador se agrupan en una acción de deshacer. Las propiedades generales están en un desplegable para priorizar los ajustes del objeto.

El límite de FPS usa tiempo monotónico y un presupuesto de envíos GPU por monitor; se consulta de nuevo la frecuencia en ejecución. El contador de escenas toma el visualizador animado, excluyendo repintados de tarjeta/reloj. Sin fotogramas recientes indica cero y una pantalla cubierta se informa con cero FPS. En dos ventanas de prueba simultáneas la Acer de 75 Hz produjo 71,5 FPS durante seis segundos, contador 73,9; el objetivo era 75. La prueba no es un benchmark del escritorio a pantalla completa.

En Windows, el modo de render optimizado descarta aristas traseras y recorta las que cruzan el plano de silueta; desactivarlo conserva la malla transparente completa. Los datos geométricos se mantienen para la rotación. Las publicaciones de macOS y Linux no cambian.

Pruebas añadidas: editor WinUI guardar/descartar, identidad retenida del renderizador, sliders sin NumberBox, historial por gesto, orden de capas, límite en las pantallas físicas y presupuesto móvil de un segundo. Los informes y la imagen del panel se conservan en los artefactos de validación.
Local validation note: the audible 1 kHz WASAPI self-test could not isolate its quiet tone while music was already playing (dominant peak 46.875 Hz). Capture delivered 181 frames at 48 kHz. No audio-capture implementation changed in this release; CI repeats this check on a clean endpoint.
