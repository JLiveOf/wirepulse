# WirePulse

Fondo de escritorio animado y reactivo al audio para **Windows, Linux y macOS**. Fondo negro, espectro LED a la izquierda, orbe de malla roja y tarjeta musical arriba centrada, con portada y proveedor.

## Descargar

**[Abrir Releases y elegir tu sistema](https://github.com/JLiveOf/wirepulse/releases/latest)**

| Sistema | Archivo de descarga | Instalación |
| --- | --- | --- |
| Windows x64 | `WirePulse-…-win-x64.zip` | Extrae la carpeta completa y abre `WirePulse.exe`. |
| Linux Mint / Cinnamon X11 x64 | `wirepulse_…_amd64.deb` | Abre el paquete con el instalador de software. |
| Linux x64, portátil | `WirePulse-…-linux-x64.tar.gz` | Extrae el archivo y ejecuta `WirePulse`. |
| macOS, Apple silicon | `WirePulse-…-macOS-arm64.zip` | Cierra WirePulse, extrae la app y llévala a Aplicaciones. |

Cada release reúne **los tres sistemas** y un archivo `SHA256SUMS.txt`. Windows/Linux son autocontenidos y no necesitan instalar .NET. Linux requiere Cinnamon **X11**, no Wayland. macOS requiere 14.2 o posterior; la app lleva firma ad hoc y no está notarizada. Windows no lleva firma comercial.

## Funciones

- Audio real de salida: WASAPI en Windows, monitor PulseAudio/PipeWire en Linux y Core Audio en macOS. Sin micrófono ni grabaciones guardadas.
- FFT adaptativa y 64 bandas continuas desde DC hasta Nyquist; análisis estéreo que conserva señales en contrafase.
- Sensibilidad, graves, suavizado, colores, tamaño y posición de los elementos.
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

Windows/Linux incluyen perfiles Automático, Calidad y Ligero, sincronización por pantalla y diagnóstico GPU/CPU. [Ver pruebas y mediciones](PERFORMANCE.md).

## Validación

GitHub Actions compila y prueba los tres sistemas antes de publicar una release única. Incluye pruebas de FFT, Cider v1/v2 con servidores locales de prueba, controles de ajustes y persistencia independiente de pantallas. Linux se comprueba con Cinnamon/Nemo, audio real de un monitor PulseAudio y datos MPRIS generados. macOS comprueba también la reubicación de una app en ejecución.

Las pruebas automáticas no sustituyen la validación de hardware de audio de Windows, monitores físicos adicionales ni un cliente Cider instalado. Las fixtures no incluyen canciones comerciales ni datos personales.
