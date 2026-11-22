# WirePulse · Windows y Linux

[English](README.md) · [Funciones y descargas](../README.es.md)

Necesitas **.NET SDK 10**. Windows requiere además el Windows SDK y las herramientas de WinUI que usa `build-windows.ps1`. Linux necesita **Cinnamon X11**, FFmpeg, herramientas de PulseAudio/PipeWire y WebKitGTK 4.1 para vídeo, audio y widgets HTML.

```powershell
# Windows: compilar la app y el complemento WinUI, y ejecutar las pruebas
./build-windows.ps1 -Test
```

```sh
# Linux: generar la app autocontenida
 dotnet publish -c Release -f net10.0 -r linux-x64 --self-contained true -o publish
 ./publish/WirePulse --self-test
 python3 packaging/package.py linux-x64 "$(cat ../VERSION)"
```

[GitHub Actions](../.github/workflows/release.yml) genera y comprueba los paquetes. Los ajustes y recursos se guardan por usuario, fuera de la carpeta de instalación.
