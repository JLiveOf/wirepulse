## Selección de motores gráficos en Windows

En **Ajustes → Rendimiento**, elige Automático, **DirectX 11**, **Vulkan**, OpenGL o CPU y pulsa **Aplicar y reiniciar**. La elección se aplica a todas las pantallas y se conserva entre aperturas.

- Vulkan/OpenGL intentan su controlador y, si no se inicializa, usan DirectX 11 y después CPU. Automático conserva DirectX 11 con alternativa por CPU.
- El diagnóstico muestra el motor realmente activo y la descripción del controlador DirectX 11/OpenGL cuando está disponible. No se etiqueta una selección solicitada como si fuera el motor activo.
- Nuevo acceso **WirePulse (modo compatible)** en Inicio y `WirePulse-SafeMode.cmd` en el ZIP para recuperar los ajustes por CPU si un controlador falla.
- Se mantienen los fondos reactivos, estrellas independientes, reloj, portadas, Cider y perfiles por pantalla. Linux y macOS conservan sus motores actuales.

**DirectX 12 no está incluido**: Avalonia no ofrece ese backend. NVENC no interviene porque el fondo se dibuja directamente, sin codificar vídeo. Cambiar de API no garantiza más FPS en todos los equipos.

### Descargas

| Sistema | Archivo |
| --- | --- |
| Windows x64 instalador | **WirePulse-2.5.0-win-x64-Setup.exe** |
| Windows x64 portátil | WirePulse-2.5.0-win-x64.zip |
| Linux Mint / Cinnamon X11 | wirepulse_2.5.0_amd64.deb |
| Linux x64 portátil | WirePulse-2.5.0-linux-x64.tar.gz |
| macOS / Apple silicon | WirePulse-2.5.0-macOS-arm64.zip |

Windows 10 2004+ x64; Linux X11; macOS 14.2+ Apple silicon. Windows sin firma comercial; macOS con firma ad hoc, sin notarización.

Las pruebas comprueban persistencia global y reinicio explícito, órdenes de alternativas, ventanas nativas con los cinco modos, FFT, Cider, ajustes, rendimiento e instalación/reinstalación/desinstalación. Vulkan puede recurrir a una alternativa en el corredor de pruebas; la validación no certifica una RTX 3050 física ni 165 FPS.
