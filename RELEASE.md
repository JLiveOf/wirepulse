WirePulse ahora reúne **Windows, Linux y macOS en un único repositorio y en esta misma página de descargas**. Esta actualización unifica el proyecto y su publicación; conserva las funciones de las versiones anteriores.

### Elige tu sistema

| Sistema | Descarga |
| --- | --- |
| **Windows x64** | **WirePulse-2.1.0-win-x64.zip** — extrae todo y abre WirePulse.exe. |
| **Linux Mint / Cinnamon X11** | **wirepulse_2.1.0_amd64.deb** — instala con el gestor de software. |
| **Linux x64 portátil** | **WirePulse-2.1.0-linux-x64.tar.gz** — extrae y ejecuta WirePulse. |
| **macOS · Apple silicon** | **WirePulse-2.1.0-macOS-arm64.zip** — extrae y mueve la app a Aplicaciones. |

Incluye fondo negro, orbe y espectro reactivos, tarjeta arriba centrada, soporte Cider y posiciones compartidas o independientes para cada pantalla. Ajustes se abre desde el icono de WirePulse en la barra de menús o área de notificación.

Cider: activa su API local y crea un token de Playback; pégalo en Ajustes → Tarjeta. No se solicitan credenciales de Apple. La integración usa datos y portadas estáticas proporcionados por el reproductor.

Windows y Linux incluyen el runtime necesario. Linux requiere Cinnamon X11; Wayland no está soportado. macOS requiere Apple silicon y macOS 14.2+, con firma ad hoc y sin notarización. Windows no está firmado comercialmente.

Los tres paquetes se compilan y comprueban antes de publicar esta release. Se incluyen pruebas de FFT, protocolo Cider, ajustes y perfiles; integración Cinnamon con PulseAudio/MPRIS y pruebas de reubicación del bundle de macOS. No se ha validado hardware Windows, Cider real ni monitores físicos adicionales.

El historial de los repositorios anteriores se conserva en este proyecto. El código fuente de las tres versiones está en los archivos Source code de esta release. `SHA256SUMS.txt` permite verificar todas las descargas.
