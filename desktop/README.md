# WirePulse · Windows and Linux

[Español](README.es.md) · [Features and downloads](../README.md)

Requires **.NET SDK 10**. Windows also needs the Windows SDK and the WinUI tooling used by `build-windows.ps1`. Linux targets **Cinnamon X11**; install FFmpeg, PulseAudio/PipeWire tools and WebKitGTK 4.1 for video, audio and HTML widgets.

```powershell
# Windows: publish, build the native WinUI companion, and run tests
./build-windows.ps1 -Test
```

```sh
# Linux: publish the self-contained app
 dotnet publish -c Release -f net10.0 -r linux-x64 --self-contained true -o publish
 ./publish/WirePulse --self-test
 python3 packaging/package.py linux-x64 "$(cat ../VERSION)"
```

Release packages are built and tested by [GitHub Actions](../.github/workflows/release.yml). Settings and assets are stored per user, outside the installation directory.
