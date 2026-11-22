# WirePulse · macOS

[English](README.md) · [Funciones y descargas](../README.es.md)

Necesitas **macOS 14.2+, Apple silicon** y Xcode o unas Command Line Tools compatibles.

```sh
./build.sh
open dist/WirePulse.app
```

Permite la captura del audio del sistema cuando lo solicite macOS. Las portadas también pueden necesitar permiso de Automatización. Ajustes → Inicio incluye inicio de sesión, idioma y actualizaciones estable/beta.

La app usa AppKit, SwiftUI, Metal y Core Audio. El script aplica una firma ad hoc; la distribución no está notarizada.
