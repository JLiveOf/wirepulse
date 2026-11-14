# Rendimiento, método de prueba y límites

## Correcciones de 2.2

La versión 2.1 tenía dos límites independientes: la app usaba un temporizador de interfaz a 120 Hz y un máximo manual de 120 FPS; Avalonia 11.3.12, además, inicializaba el reloj de Win32 a 60 Hz. El nuevo código usa solicitudes de fotograma del sistema y Avalonia 12.1.6, que ajusta su reloj de Windows a la frecuencia máxima de las pantallas. Un contador de plazos mantiene la fracción sobrante cuando el objetivo no divide la frecuencia del reloj. Por ejemplo, limitar a 60 desde 165 Hz no debe convertir el resultado en 55 FPS.

Cada pantalla tiene su propio contador y objetivo. En Windows la frecuencia se consulta al monitor que contiene su rectángulo físico; en X11 se consulta el modo activo de xrandr. El objetivo no garantiza la presentación de todos los fotogramas: depende del hardware, controladores, compositor y carga del sistema.

El orbe/LED se dibuja mediante una operación Skia en el hilo de renderizado. Se reutilizan vértices, conexiones, pinturas, clips y caminos. En GPU se envían lotes de segmentos visibles; el fallback por CPU reutiliza caminos. Fondo y tarjeta tienen capas propias; el texto de la tarjeta se conserva y su progreso se actualiza cuatro veces por segundo. Las portadas se decodifican a 512 píxeles y se reutilizan; el audio usa una FFT compleja para recuperar la potencia de dos canales reales independientes.

La malla tiene un único vértice en cada polo y una envolvente suave de deformación. Las pruebas comprueban que no aparecen aristas polares de longitud cero y que el sonido no separa los polos para crear agujas.

## Perfiles

- **Automático:** conserva el aspecto inicial y reduce gradualmente la densidad y el resplandor cuando aumenta el coste de dibujo o faltan FPS.
- **Calidad:** respeta la densidad y el resplandor elegidos, sin adaptación.
- **Ligero:** malla de hasta 24 divisiones, sin resplandor, hasta 60 FPS. Es el punto de partida para equipos antiguos o sin aceleración gráfica.

Windows puede suspender el dibujo de cada pantalla mientras una aplicación ocupa toda su superficie. Se configura por pantalla. La lectura de audio y los metadatos siguen disponibles para reanudar el fondo.

## Comparación reproducible

Desde `desktop/`:

```
dotnet run --project tests/Performance -c Release -- test-results/performance.json
dotnet run --project tests/UiTests -c Release
dotnet run -c Release -f net10.0 -- --self-test
```

El benchmark conserva el dibujo anterior en `tests/Performance/LegacyWallpaperView.cs` y la FFT anterior en `LegacySpectrum.cs`. Ambos renderizadores se ejecutan en el mismo proceso con el mismo framework y rasterización **por CPU**, sin GPU. Se usan 12 fotogramas de calentamiento y 80 medidos por caso, una pose fija y audio sintético fresco antes de cada dibujo. El coste de generar/analizar ese audio se excluye del tiempo del renderizador y se mide por separado. Los JSON y una imagen de referencia quedan disponibles como artefactos de GitHub Actions en Windows y Linux.

Medición local orientativa en macOS arm64; son costes de dibujo offscreen, **no FPS de una RTX 3050 ni de un PC de 2012**:

| Caso | Dibujo anterior | Calidad nueva | Ligero nuevo |
| --- | ---: | ---: | ---: |
| 1920×1080, esquina | 5,79 ms | 4,92 ms | 1,23 ms |
| 1440×900, esquina | 3,97 ms | 3,81 ms | 0,75 ms |
| 3840×2160, esquina | 10,38 ms | 10,53 ms | 3,73 ms |
| 1920×1080, orbe centrado | 19,94 ms | 19,56 ms | 1,72 ms |

El perfil Calidad no mejora todos los casos por CPU; a 4K el coste es prácticamente igual dentro de la variación de la prueba. Ligero reduce mucho más la carga al modificar explícitamente el detalle y el resplandor. Las asignaciones administradas del benchmark bajan de unos 218–220 KB a unos 16–19 KB por fotograma. El coste del texto está incluido; el programa en vivo conserva esa capa entre actualizaciones.

A 48 kHz, el análisis pasa aproximadamente de 24,2 a 10,6 ms por segundo de audio. Se comparan dos FFT reales independientes con la recuperación estéreo empaquetada usando señal aleatoria a 44,1/48/96/192 kHz; la diferencia máxima observada entre bandas es inferior a 7×10⁻¹⁴. Se mantienen las pruebas de contrafase, DC, Nyquist, silencio y cobertura de frecuencias.

## Diagnóstico en el equipo real

Ajustes → Audio y Rendimiento muestra los FPS de dibujo, objetivo, tiempo medio del orbe/LED y si Skia está usando GPU o CPU. `status.json` añade frecuencia y perfil por pantalla, suspensión, CPU y memoria del proceso. Los FPS se cuentan al ejecutar el dibujo; no son una medición del fotograma finalmente presentado por DWM.

La validación automática incluye el programa con Cinnamon/Nemo y captura real de un monitor PulseAudio, además de compilación/pruebas en Windows y macOS. Para confirmar 165 Hz se necesita medirlo en un equipo con esa pantalla. No se garantiza fluidez universal en cualquier ordenador de 2012; el sistema operativo, GPU, resolución y controladores también deben ser compatibles.


## Medición adicional en Windows CI (CPU, sin RTX física)

En el ejecutor de Windows, 1080p pasó de 8,86 ms/fotograma a 7,91 ms en Calidad y 1,67 ms en Ligero. Las asignaciones bajaron de 222.758 a 17.370 bytes por fotograma en Calidad. La FFT a 48 kHz pasó de 21,21 a 10,98 ms por segundo de audio. Los valores dependen del ejecutor y no equivalen a un i5-12400F/RTX 3050.

También se prueba la firma binaria de la operación de líneas de Skia y que los puntos fuera de la porción enviada no se dibujan, incluso en CI sin GPU física. Las nuevas aperturas de la app reutilizan la instancia existente y sus ajustes; hay pruebas entre procesos para excluir capturas/renderizadores duplicados y comprobar recuperación tras un cierre inesperado.


## Fondo y reloj (2.4)

El banco de pruebas conserva los tres casos anteriores con los elementos nuevos desactivados y añade `stars` y `mesh` a 1080p con el reloj visible. El fondo reutiliza caminos y búferes; el reloj se invalida a 1 Hz y conserva el texto formateado. Ligero limita las estrellas a 80 y la separación mínima de la malla a 120 px. El análisis de tempo se comparte entre pantallas y se actualiza dos veces por segundo, con una ventana de ocho segundos.

En la prueba local por CPU de macOS/Skia, la escena 1080p de referencia midió 4,90 ms por cuadro; con estrellas 4,76 ms, y con malla 4,94 ms. La variación entre pasadas impide interpretar diferencias pequeñas como una mejora. Estos valores no son una medición de la RTX 3050 ni garantizan una frecuencia de refresco física.


## Selección de motor en Windows (2.5)

El selector cambia `Win32PlatformOptions.RenderingMode` antes de inicializar Avalonia. DirectX 11 corresponde a ANGLE EGL; Vulkan usa el backend nativo; OpenGL usa WGL; CPU fuerza Software. La preferencia se guarda globalmente y requiere reinicio. No modifica el perfil de calidad ni la configuración de cada monitor.

El nombre activo procede del contexto de plataforma y de `GRContext.Backend`, leídos bajo un préstamo de la API gráfica en el primer dibujo. Para ANGLE/WGL se consulta `GL_RENDERER` dentro de ese contexto. Así se puede distinguir la selección pedida de su alternativa y reconocer un controlador WARP por su descripción, sin llamarlo RTX u otra GPU por suposición. `status.json` incluye `rendererRequested` y `rendererActive`.

La CI de Windows abre una ventana nativa con cada selección, exige fotogramas dibujados y verifica que el motor activo pertenezca al orden de alternativas. La prueba por CPU debe permanecer en CPU. Los informes de `windows-renderers` registran las alternativas efectivamente utilizadas. Estas pruebas no certifican Vulkan con una RTX 3050 física ni una mejora de FPS; el corredor puede carecer de un controlador Vulkan y usar una alternativa.
