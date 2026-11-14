Mejoras de rendimiento y correcciones para Windows y Linux. macOS mantiene su implementación nativa, con el mismo número de versión para la publicación conjunta.

- Sincronización con la frecuencia de cada pantalla: se eliminan el tope de 120 FPS y el reloj fijo de 60 Hz de la biblioteca anterior. Un monitor de 165 Hz puede solicitar 165 FPS y otro de 75 Hz, 75, según el margen del equipo.
- Se corrigen los saltos de cadencia al limitar los FPS. El dibujo sigue los fotogramas del sistema, sin temporizador de interfaz fijo.
- Orbe por lotes en el hilo de dibujo, con buffers reutilizados y menos objetos temporales. Fondo y tarjeta se conservan en capas separadas; el texto no se reconstruye con cada fotograma del orbe.
- Polos de la malla corregidos y deformación suavizada: se elimina la «aguja» que aparecía en Windows/Linux al sonar música.
- Perfiles Automático, Calidad y Ligero. Ligero reduce la malla, elimina el resplandor y usa hasta 60 FPS. Automático ajusta el detalle si aumenta el coste de dibujo.
- Suspensión automática cuando una aplicación ocupa toda la pantalla (Windows), configurable por pantalla.
- FFT estéreo optimizada, manteniendo la cobertura DC–Nyquist y la energía en contrafase. Caché de portadas de Windows y progreso corregido; se reintentan portadas que llegan tarde.
- FPS de dibujo, objetivo, backend GPU/CPU y tiempo de dibujo visibles en Ajustes → Audio / Rendimiento y en status.json.

### Descargas

| Sistema | Archivo |
| --- | --- |
| Windows x64 | **WirePulse-2.2.0-win-x64.zip** |
| Linux Mint / Cinnamon X11 | **wirepulse_2.2.0_amd64.deb** |
| Linux x64 portátil | **WirePulse-2.2.0-linux-x64.tar.gz** |
| macOS / Apple silicon | **WirePulse-2.2.0-macOS-arm64.zip** |

Extrae por completo el ZIP de Windows y abre WirePulse.exe. En Ajustes → Rendimiento activa «Adaptarse a la frecuencia de cada pantalla». Prueba Ligero en equipos antiguos. Las preferencias y perfiles existentes se conservan.

Las mediciones de rendimiento se hacen con rasterización por CPU y se comparan con el dibujo anterior, en el mismo proceso. Son pruebas reproducibles, no una garantía de 165 FPS en cualquier equipo ni una validación de hardware de 2012. Las pruebas de integración, audio y ajustes se ejecutan antes de publicar. Consulta PERFORMANCE.md para el método y los resultados.

Linux requiere X11, no Wayland. macOS 14.2+, Apple silicon; firma ad hoc, sin notarización. Windows no está firmado comercialmente. Cider, portadas estáticas y ajustes independientes por pantalla siguen disponibles.
