## Estrellas independientes y etiqueta del reproductor centrada

- **Estrellas**: puntos redondeados con velocidad, profundidad, trayectoria y vaivén propios, inspirados en los indicadores de movimiento del iPhone. Mantienen la dirección general elegida y aceleran suavemente con el audio.
- **Cider**: si los metadatos de la canción incluyen BPM válidos, se aplican al fondo y se identifican en Ajustes. Si faltan, son inválidos, Cider pausa o los datos caducan, se conserva la detección por audio. Es compatibilidad condicional: no se ha confirmado que todas las versiones de su API publiquen ese campo.
- **Windows/Linux**: texto del proveedor centrado horizontal y verticalmente en su etiqueta, respetando el tamaño de la tarjeta.
- Se mantienen Malla, reloj, ajustes por pantalla, instalador de Windows e inicio automático opcional.

### Descargas

| Sistema | Archivo |
| --- | --- |
| Windows x64 instalador | **WirePulse-2.4.1-win-x64-Setup.exe** |
| Windows x64 portátil | WirePulse-2.4.1-win-x64.zip |
| Linux Mint / Cinnamon X11 | wirepulse_2.4.1_amd64.deb |
| Linux x64 portátil | WirePulse-2.4.1-linux-x64.tar.gz |
| macOS / Apple silicon | WirePulse-2.4.1-macOS-arm64.zip |

Windows 10 2004+ x64; Linux requiere X11; macOS 14.2+ Apple silicon. Windows sin firma comercial y macOS con firma ad hoc, sin notarización.

Se comprueban trayectorias independientes, pausa, BPM opcionales v1/v2 de Cider, valores ausentes e inválidos, prioridad y caducidad de metadatos, FFT, detección de tempo, ajustes, rendimiento e instalación/reinstalación/desinstalación de Windows. La lectura de BPM se valida con respuestas simuladas; no garantiza que un cliente Cider concreto incluya ese dato.
