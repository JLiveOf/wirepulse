# Native Windows settings and editor

[Español](README.es.md) · [Build WirePulse](../README.md)

This WinUI 3 companion provides settings and the desktop editor controls. The main WirePulse process owns the scene, preferences, rendering and audio. They communicate through a per-user named pipe.

Build both applications with `../build-windows.ps1 -Test`. Keep the published `WindowsSettings` directory, its executable and its `.pri` file together with WirePulse.
