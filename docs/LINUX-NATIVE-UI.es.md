# Interfaz de Linux Mint / Cinnamon

El hotfix de Linux 4.5.1 utiliza GTK 3 para ajustes, bienvenida, novedades, actualizaciones y los paneles del editor. Respeta el tema GTK instalado; Mint-Y no se incluye ni se fuerza en los equipos de los usuarios. El fondo, el audio y el lienzo de edición conservan el motor de WirePulse.

Comparte con las otras versiones el catálogo de controles, los archivos de configuración y layout, los límites de cada propiedad y el historial del editor. Las funciones específicas del sistema, como Direct3D en Windows o los permisos de audio de macOS, conservan su implementación de plataforma.

Los selectores de archivos, colores y fuentes son GTK. El editor incluye capas arrastrables, bloqueo y visibilidad por objeto, eliminación, duplicación, deshacer/rehacer, guías, rotación, opacidad, texto y gradientes. Los cambios se envían al mismo borrador del lienzo; no se escribe directamente la configuración desde Python.

Las ventanas caben en el área útil del monitor, con contenido desplazable. Novedades usa el fondo del tema de Mint para mantener el contraste; no depende de una simulación de Liquid Glass ni de extensiones de desenfoque del compositor. La splash se transforma en la misma ventana de configuración/novedades, y el cierre espera al efecto de tele antes de destruirla.

## Instalación

El paquete `.deb` declara las dependencias de la interfaz y es la opción recomendada para Mint. En la versión portable, además de las dependencias de audio y vídeo de WirePulse, se necesitan:

```sh
sudo apt install python3-gi python3-gi-cairo gir1.2-gtk-3.0 librsvg2-common
```

## Validación

El workflow `linux-hotfix.yml` ejecuta Cinnamon/Nemo bajo Xvfb, captura PulseAudio real, prueba MPRIS y utiliza los archivos GTK del tema oficial Mint-Y fijados a un commit. La interfaz GTK prueba sus señales con el renderizador .NET real: controles compartidos, propiedades, GIF, orden de capas, historial, bloqueo, guías, guardado y cierre de novedades. Sus capturas y resultados quedan en el artefacto `linux-hotfix-validation`. También comprueba que el protocolo compartido siga siendo compatible con WinUI.

Este hotfix sustituye únicamente las descargas de Linux, sus identificadores y checksums en la release 4.5.1. Los paquetes de Windows y macOS se conservan.
