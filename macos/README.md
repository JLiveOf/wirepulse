> **Repositorio unificado:** [WirePulse](https://github.com/JLiveOf/wirepulse) · [Descargas para todos los sistemas](https://github.com/JLiveOf/wirepulse/releases/latest)

# WirePulse para macOS

Port nativo del fondo WirePulse v2 de Windows / Wallpaper Engine. Usa Swift, AppKit, SwiftUI, Metal y Core Audio process taps. No usa navegador, Electron, Wallpaper Engine, un micrófono ni un controlador de audio externo.

## Usar

Abre `WirePulse.app`. La app dibuja en una ventana de escritorio detrás de los iconos y de las ventanas normales, dejando pasar los clics a Finder. Se crea una superficie por pantalla y se muestra en los escritorios de Spaces. Al salir, vuelve a verse el fondo normal de macOS; no se modifica su configuración de wallpaper.

La aplicación permanece en la barra de menús mediante un icono de onda. Allí puedes abrir Ajustes, pausar la animación, reiniciar el audio o salir. Para mantener el fondo animado, deja la app abierta. No se ha añadido un elemento de inicio de sesión.

macOS puede pedir permiso de grabación de audio del sistema. Concédelo para que los visualizadores respondan al sonido. El audio se procesa en memoria mediante una FFT adaptativa de 8192 a 65536 muestras y 64 bandas visuales que cubren desde DC (0 Hz) hasta Nyquist (la mitad de la frecuencia real de muestreo). No se guarda una grabación. El dispositivo agregado y el tap son privados al proceso; no se cambia la salida de audio del Mac.

En Ajustes → Tarjeta puedes elegir Apple Music, Spotify o Automático para consultar los datos disponibles de la reproducción. La etiqueta de la tarjeta muestra el proveedor en lugar de «AUDIO». Esto solicita permiso de Automatización. La portada de Spotify se descarga de la URL de imagen que proporciona Spotify. Esta integración es opcional: el audio del sistema y los visualizadores funcionan sin activarla. Si no eliges una fuente, la tarjeta indica «Audio del sistema» y no inventa el título de una canción.

## Apariencia y controles

Se portaron los 35 controles del proyecto original: color e imagen de fondo, oscurecimiento, sensibilidad, graves, puerta de ruido, suavizado, visibilidad y geometría del orbe, color, tamaño, posición, deformación, pulso, rotación, densidad, grosor, resplandor, opacidad, espectro, posiciones y tamaño, LED segmentados, tarjeta, ocultación en reposo, posición y escala, acento, FPS y resolución de dibujo.

Los valores iniciales/restablecidos son fondo negro `#000000` y espectro en toda la altura izquierda (x=0, y=0, altura=100%). La geometría del orbe y sus trazos interiores siguen la implementación original, recortados en la esquina inferior derecha. El análisis de audio se comparte entre los LED y el orbe; con silencio, la energía decae según el suavizado seleccionado.

Las preferencias se guardan en el dominio `dev.wirepulse.macos`. Una imagen de fondo elegida se copia a `~/Library/Application Support/WirePulse/`. El archivo `status.json` de esa carpeta contiene contadores de renderizado/FFT y estado de captura, sin muestras de audio ni datos de canciones.

La animación y la captura se detienen al dormir la pantalla o bloquear/cambiar la sesión y se reanudan al volver. La app es un fondo de escritorio implementado por una aplicación nativa; no es un proveedor registrado en el panel Fondos de pantalla de Ajustes del Sistema ni un archivo HEIC.

## Compilar

Requiere Xcode/Command Line Tools con Swift y un SDK que incluya Core Audio process taps. Objetivo actual: macOS 14.2 o posterior, Apple silicon (arm64). Compilado y probado con macOS 27.2, Xcode 27.2 y Swift 6.4.2, en modo de lenguaje Swift 5.

```sh
./build.sh
open dist/WirePulse.app
```

La compilación usa los frameworks del sistema, sin dependencias externas. Firma ad hoc para ejecución local; no está notarizada para distribución pública. Para elegir otra carpeta de salida:

```sh
WIREPULSE_OUTPUT_DIR=/ruta/a/salida ./build.sh
```

El bundle se crea a partir de `Resources/Info.plist`, que contiene las descripciones de uso de audio y automatización.

## Comprobar el analizador

```sh
dist/WirePulse.app/Contents/MacOS/WirePulse --self-test
codesign --verify --deep --strict dist/WirePulse.app
```

El test verifica tonos de 20 Hz, 1 kHz y cerca de Nyquist con PCM a 44,1/48/96/192 kHz, cobertura continua de las 64 bandas, estéreo en contrafase, silencio, DC y Nyquist. Usa comprobaciones activas en la compilación optimizada.

`--render-preview /ruta/imagen.png` genera una imagen mediante Metal desde una señal PCM sintética de tres tonos. No es una captura del escritorio y no incluye la tarjeta SwiftUI.

## Límites conocidos

- La captura analiza el PCM que entrega Core Audio. Un archivo lossless puede tener una frecuencia de muestreo distinta de la salida del sistema; no se afirma captura bit-perfect ni se modifica la calidad de reproducción.
- Las 64 barras agrupan los bins de la FFT; no son mediciones independientes de cada Hz.
- La integración de Apple Music muestra la portada estática real. La automatización usada no expone la animación del álbum ni un estado fiable de AutoMix en curso.
- En modo Automático, los servicios web se identifican a partir de la pestaña activa del navegador con salida de audio. Con varias pestañas, esa pestaña puede no ser la que emite audio. Si no hay acceso o identificación fiable, se muestra la aplicación.
- Las portadas y el progreso de servicios web no se inventan. Firefox, Arc y procesos WebKit genéricos se identifican por aplicación sin consultar sus URLs.
- Si un cambio de dispositivo interrumpe el flujo, usa «Reiniciar audio del sistema».

## Icono

Icono propio adaptado del icono de OrbMusic para iPod: orbe de alambre rojo y LED sobre fondo oscuro, con máscara y márgenes para macOS. `Resources/WirePulse.icns` contiene tamaños de 16 a 1024 píxeles y se incorpora al bundle al compilar. La aplicación no necesita dependencias para usarlo. Para regenerarlo opcionalmente: `bun install`, `bun make-icon.ts` y `iconutil -c icns Resources/WirePulse.iconset -o Resources/WirePulse.icns`.

## Cambios de la versión 1.1.0

- Sin límite artificial de 18 kHz: se incluye cada bin de la FFT, desde DC hasta Nyquist, sin huecos ni bins duplicados entre las bandas visuales. DC ocupa una banda; las otras 63 agrupan los bins positivos. Las 64 barras son bandas agregadas, no mediciones independientes de cada Hz.
- FFT adaptativa: 8192 muestras a 44,1/48 kHz, 16384 a 96 kHz, 32768 a 192 kHz; hasta 65536 si el flujo requiere más resolución. Aproximadamente 5–6 Hz por bin en esas frecuencias, con ventana Hann y actualizaciones cercanas a 60 Hz. Esta precisión aumenta la duración temporal de la ventana (unos 171 ms a 48 kHz).
- Se analizan izquierdo y derecho por separado y se combina su potencia: la contrafase no cancela la energía del espectro. El refuerzo y pulso de graves usan el intervalo físico de 20–160 Hz.
- Se observa el formato del tap y se reinicia la captura si cambia. Se comprobó captura real a 48000 Hz: límite espectral 24000 Hz y resolución 5,859375 Hz/bin. El test a 96/192 kHz usa PCM sintético; no se verificó hardware de captura a esas frecuencias.
- Un archivo lossless no garantiza un flujo de captura a su frecuencia original. Se analiza el PCM Float32 que entrega Core Audio; no se afirma captura bit-perfect del archivo ni se mejora la calidad de reproducción.
- La portada de Apple Music se lee desde los datos binarios del descriptor Apple Event, mediante un proceso auxiliar del mismo ejecutable que ejecuta AppleScript en su hilo principal. Se mantiene una caché en memoria y se reintenta cada seis segundos si Apple Music todavía no ha cargado la imagen. La clave incluye proveedor, identificador de canción, título, artista y álbum, evitando confundir canciones homónimas.
- La animación del álbum de Apple Music no está expuesta por esta integración de automatización. Se muestra la portada estática real. `MPMediaItemAnimatedArtwork` sirve para proporcionar una animación al sistema, no para extraer la que reproduce otra aplicación; `SystemMusicPlayer` de MusicKit figura como no disponible en macOS en el SDK usado. No se añadió una animación artificial.
- Modo Automático: consulta Apple Music y Spotify, priorizando el que indica reproducción; si no hay uno reproduciendo, consulta los procesos con salida de audio activa a través de Core Audio. Si detecta un navegador compatible, lee su pestaña activa mediante Automatización e identifica YouTube, YouTube Music, Apple Music web, Spotify web, SoundCloud, TIDAL o Deezer según el dominio. Si no puede identificar el servicio, muestra el navegador o aplicación. Con varias pestañas la pestaña activa puede no ser la que emite audio: es una identificación orientativa, no una inspección exacta de sesiones por pestaña. No se promete detección universal.
- No se añaden portadas ni tiempos de reproducción inventados al navegador. Firefox, Arc y procesos WebKit genéricos muestran el nombre de la aplicación sin consultar su URL. Safari, Chrome, Edge y Brave pueden solicitar permiso de Automatización cuando se selecciona Automático. La selección manual existente se conserva.

Referencias de Apple: [formato real del tap](https://developer.apple.com/documentation/coreaudio/audiohardwaretap/format), [portadas animadas](https://developer.apple.com/documentation/mediaplayer/mpmediaitemanimatedartwork).

## Corrección 1.1.1: mover la aplicación

El lector de portadas usa la ruta actual del ejecutable obtenida del kernel con `proc_pidpath`, en lugar de depender de `Bundle.main.executableURL`, que conserva la ruta del lanzamiento. Así puede abrir el proceso auxiliar aunque Finder haya movido la app mientras seguía ejecutándose. La prueba `python3 scripts/test-relocation.py dist/WirePulse.app` crea una copia desechable, mueve su bundle mientras se ejecuta y comprueba el lanzamiento de un proceso hijo desde la nueva ubicación, sin modificar la app original.


## WirePulse 1.2 · Cider y varias pantallas

La tarjeta aparece arriba centrada por defecto. En Ajustes puedes mantener la misma disposición proporcional en todas las pantallas o activar «Ajustar cada pantalla por separado» y elegir la pantalla que quieres configurar. Los perfiles se guardan con el identificador de la pantalla; audio y canción se comparten.

Cider se incluye como proveedor explícito y en Automático mediante su API local de solo lectura. En Cider activa Connectivity / API, crea un token con permiso Playback y pégalo en Ajustes → Tarjeta. Dirección predeterminada: `http://127.0.0.1:10767`. Se usa el snapshot v2 y se conserva compatibilidad con v1. El token se guarda en el Llavero; las peticiones no siguen redirecciones y solo admiten loopback. Portadas estáticas desde la URL publicada por Cider. No se solicitan credenciales de Apple ni acceso a la biblioteca.

Referencia oficial: https://taproom.cider.sh/docs/api/.

Pruebas adicionales: `python3 scripts/test-cider.py dist/WirePulse.app/Contents/MacOS/WirePulse` valida respuestas v2/v1 contra un servidor local de prueba; `--display-profile-test` comprueba persistencia aislada de perfiles. El cliente real de Cider y monitores físicos adicionales requieren comprobación en esos equipos.

Versiones con renderizador compartido para [Windows](https://github.com/JLiveOf/wirepulse) y [Linux Mint / Cinnamon X11](https://github.com/JLiveOf/wirepulse).
