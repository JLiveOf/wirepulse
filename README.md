# WirePulse

Fondo de escritorio animado y reactivo al audio para **Windows, Linux y macOS**. Fondo negro, espectro LED a la izquierda, orbe de malla roja y tarjeta musical arriba centrada, con portada y proveedor.

## Descargar

**[Abrir Releases y elegir tu sistema](https://github.com/JLiveOf/wirepulse/releases/latest)**

| Sistema | Archivo de descarga | Instalación |
| --- | --- | --- |
| Windows x64 instalador | `WirePulse-...-win-x64-Setup.exe` | Instala para tu cuenta, con acceso en Inicio y desinstalador. |
| Windows x64 portable | `WirePulse-…-win-x64.zip` | Extrae la carpeta completa y abre `WirePulse.exe`. |
| Linux Mint / Cinnamon X11 x64 | `wirepulse_…_amd64.deb` | Abre el paquete con el instalador de software. |
| Linux x64, portátil | `WirePulse-…-linux-x64.tar.gz` | Extrae el archivo y ejecuta `WirePulse`. |
| macOS, Apple silicon | `WirePulse-…-macOS-arm64.zip` | Cierra WirePulse, extrae la app y llévala a Aplicaciones. |

Cada release reúne **los tres sistemas** y un archivo `SHA256SUMS.txt`. Windows/Linux son autocontenidos y no necesitan instalar .NET. Linux requiere Cinnamon **X11**, no Wayland. macOS requiere 14.2 o posterior; la app lleva firma ad hoc y no está notarizada. Windows no lleva firma comercial.

## Funciones

- Audio real de salida: WASAPI en Windows, monitor PulseAudio/PipeWire en Linux y Core Audio en macOS. Sin micrófono ni grabaciones guardadas.
- FFT adaptativa y 64 bandas continuas desde DC hasta Nyquist; análisis estéreo que conserva señales en contrafase.
- Sensibilidad, graves, suavizado, colores, tamaño y posición de los elementos.
- Fondo infinito ocultable: **Estrellas** (primera opción) o **Malla** triangular, con el material de alambre del orbe. Velocidad, dirección, densidad, separación, grosor, color y opacidad configurables.
- Estrellas redondeadas con trayectoria, velocidad y vaivén independientes, inspiradas en los indicadores de movimiento del iPhone. Mantienen la dirección general elegida.
- Beats detectados desde el audio aceleran suavemente el fondo; el tempo estimado modula la intensidad. En silencio continúa a la velocidad base, por defecto desde arriba a la derecha hacia abajo a la izquierda.
- Reloj ocultable con fuentes instaladas, formato de 12/24 horas, color, tamaño y posición por pantalla.
- Música, portada, progreso y proveedor desde los datos publicados por los reproductores.
- Cider en los tres sistemas, con API local v2 y compatibilidad v1. En Cider activa Connectivity/API y crea un token Playback; añádelo en Ajustes → Tarjeta. Dirección habitual: `http://127.0.0.1:10767`.
- Misma composición proporcional en todas las pantallas o perfiles independientes por pantalla, seleccionables desde Ajustes.

La identificación de servicios y las portadas dependen de los metadatos publicados por cada reproductor. Las portadas son estáticas. Consulta los detalles y límites de cada implementación antes de compilar.

## Código y compilación

| Carpeta | Implementación | Documentación |
| --- | --- | --- |
| [`desktop/`](desktop/) | Windows/Linux · C# / Avalonia / Skia; integración nativa con cada sistema | [Compilar y comprobar](desktop/README.md) |
| [`macos/`](macos/) | macOS · Swift / SwiftUI / Metal / Core Audio | [Compilar y comprobar](macos/README.md) |
| [`scripts/`](scripts/) | Versionado y comprobaciones de publicación | [Flujo de publicación](CONTRIBUTING.md) |

Los tres proyectos anteriores se han integrado conservando su historial. `desktop/` mantiene una sola copia compartida del código de Windows y Linux. Los repositorios anteriores quedan archivados como referencia; el desarrollo y las descargas actuales están aquí.

## Rendimiento

Windows/Linux incluyen perfiles Automático, Calidad y Ligero, sincronización por pantalla y diagnóstico GPU/CPU. Windows añade **Ajustes → Rendimiento → Motor gráfico**: Automático, DirectX 11 (ANGLE), Vulkan, OpenGL y CPU, con botón para aplicar y reiniciar. El motor es global para todas las pantallas. [Ver pruebas y mediciones](PERFORMANCE.md).

## Validación

GitHub Actions compila y prueba los tres sistemas antes de publicar una release única. Incluye pruebas de FFT, Cider v1/v2 con servidores locales de prueba, controles de ajustes y persistencia independiente de pantallas. Linux se comprueba con Cinnamon/Nemo, audio real de un monitor PulseAudio y datos MPRIS generados. macOS comprueba también la reubicación de una app en ejecución.

Las pruebas automáticas no sustituyen la validación de hardware de audio de Windows, monitores físicos adicionales ni un cliente Cider instalado. Las fixtures no incluyen canciones comerciales ni datos personales.


## Fondo animado y reloj

En **Ajustes → Fondo animado**, elige Estrellas o Malla y activa o desactiva el fondo y su respuesta al ritmo. La dirección se expresa en grados: 0 hacia la derecha, 90 hacia abajo y 135 en diagonal hacia abajo a la izquierda. El movimiento se integra sin saltos cuando cambia la velocidad. Los BPM se estiman a partir de ataques y correlación del audio entre 40 y 240 BPM; algunos temas pueden producir estimaciones a mitad o doble tempo. No depende de que el reproductor publique BPM. Si los metadatos de la canción que entrega Cider incluyen BPM válidos, se priorizan mientras reproduce y se indica «BPM · Cider». Cuando faltan, pausa o los datos quedan desactualizados, se utiliza el análisis del audio. Esta lectura es condicional: no se ha confirmado un campo de BPM garantizado en todas las versiones de la [API de Cider](https://taproom.cider.sh/docs/api/).

En **Ajustes → Reloj**, elige una fuente instalada, color y formato. Ambos elementos respetan la composición compartida o independiente de cada pantalla. El perfil Ligero de Windows/Linux reduce también la densidad del nuevo fondo.

## Inicio automático

La primera apertura en Windows, Linux y macOS pregunta si deseas abrir WirePulse al iniciar sesión. No se activa sin elegirlo. Puedes cambiarlo en **Ajustes → Inicio**. El inicio automático abre el fondo sin mostrar los ajustes; en macOS está sujeto a la aprobación del sistema.


### Motores de Windows

Automático conserva DirectX 11 y su alternativa por CPU. Vulkan y OpenGL intentan primero el controlador correspondiente, después DirectX 11 y por último CPU. El diagnóstico muestra el contexto realmente usado y, en DirectX 11/OpenGL cuando está disponible, el nombre que comunica el controlador. Un contexto GPU puede usar un controlador por software; no equivale por sí solo a una GPU física.

El instalador añade **WirePulse (modo compatible)** al menú Inicio. El ZIP incluye `WirePulse-SafeMode.cmd`; ambos fuerzan CPU para recuperar los ajustes si un controlador impide abrir la app. Desde allí selecciona otro motor y pulsa Aplicar y reiniciar. Esta apertura no sobrescribe tu preferencia por su cuenta.

El [motor de ventanas utilizado](https://api-docs.avaloniaui.net/docs/T_Avalonia_Win32RenderingMode) no ofrece DirectX 12; no se anuncia una selección que usaría DirectX 11 por debajo. [NVENC](https://developer.nvidia.com/video-codec-sdk) codifica vídeo: WirePulse dibuja la escena directamente y no tiene una etapa de codificación a la que aplicarlo. Cambiar de API no garantiza más FPS; depende del controlador, la GPU y el coste de preparar la escena.


En Windows 11 con el escritorio elevado, CPU/OpenGL usan una capa de presentación compatible para evitar un fondo invisible. Esa ruta copia el fotograma completo y puede rendir menos que Vulkan; no cambia el motor elegido por su cuenta. Las pruebas verifican también píxeles visibles sobre Explorer, no solo que se abra una ventana de ajustes.

### Manual desktop editor

WirePulse 3 adds drag/resize composition editing, optional snapping, independent object copies, custom text, images, animated GIFs, and HTML widgets on all three systems. Open **Editar escritorio…** from Settings or the tray/menu bar. See [Editor guide and widget format](docs/EDITOR.md).
