# Desarrollo y publicación

Todo el desarrollo de WirePulse vive en este repositorio. Windows/Linux comparten `desktop/`; el código nativo de macOS está en `macos/`.

## Publicar los tres sistemas juntos

1. Actualiza `VERSION` con un número `major.minor.patch` y las notas de `RELEASE.md`.
2. Ejecuta `python3 scripts/version.py`. Comprueba con `python3 scripts/version.py --check`.
3. Confirma los cambios y crea una etiqueta `v` seguida de la versión, por ejemplo `v2.0.2`.
4. Sube el commit y la etiqueta. El flujo `release.yml` compila y comprueba Windows, Linux y macOS.
5. Solo cuando los tres trabajos pasan, un trabajo final publica **una única release** con los archivos de los tres sistemas y sus checksums SHA-256.

Los commits a `main` ejecutan las mismas comprobaciones y guardan artefactos, pero no crean releases. No publiques manualmente archivos de una plataforma como una release independiente.
