## Corrige el fondo invisible con CPU y OpenGL en Windows

- El fondo en **CPU** y **OpenGL** utiliza una capa de presentación compatible con el escritorio moderno de Windows. La ventana del renderizador conserva su contexto gráfico y su DC, y no se fuerza una combinación de estilos incompatible con ese dibujo.
- El ejecutable declara la compatibilidad de Windows necesaria para las capas hijas del escritorio.
- Se mantienen tarjeta musical, portada, reloj, estrellas, espectro y ajustes por pantalla. **Vulkan y DirectX 11 conservan su ruta directa**.
- Se reutilizan los búferes y se agrupan las presentaciones. OpenGL tiene una copia adicional GPU→CPU para esta compatibilidad; Vulkan puede seguir siendo la opción más rápida.
- La regresión comprueba que se ven píxeles del fondo integrado en el escritorio real de Explorer, además de comprobar ventanas normales. El informe distingue el motor seleccionado del utilizado; el corredor puede usar DirectX 11 como alternativa si carece de WGL o Vulkan nativos.

### Descargas

| Sistema | Archivo |
| --- | --- |
| Windows x64 instalador | **WirePulse-2.5.1-win-x64-Setup.exe** |
| Windows x64 portátil | WirePulse-2.5.1-win-x64.zip |
| Linux Mint / Cinnamon X11 | wirepulse_2.5.1_amd64.deb |
| Linux x64 portátil | WirePulse-2.5.1-linux-x64.tar.gz |
| macOS / Apple silicon | WirePulse-2.5.1-macOS-arm64.zip |

Windows 10 2004+ x64; Linux X11; macOS 14.2+ Apple silicon. Windows sin firma comercial; macOS con firma ad hoc, sin notarización.

La comprobación de CI no sustituye la prueba de OpenGL con el controlador NVIDIA del equipo del usuario. No se modifica automáticamente el renderizador elegido: si Vulkan te funciona mejor, puedes mantenerlo seleccionado.
