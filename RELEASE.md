# WirePulse 4.2.0

- Matching welcome experience on Windows, macOS and Linux: a centered logo and name with the welcome sound, followed by an animated transition to initial settings.
- Choose login startup, automatic update checks and stable/beta channel. Existing choices are preserved until explicitly saved. Windows also retains its optional priority startup choice.
- On macOS, an audio permission step precedes the splash. Continue becomes available after real system-audio samples arrive, including during silence. The window provides retry and System Settings controls.
- Welcome runs once per installed version, including upgrades, and is acknowledged only after completing setup. Closing it keeps setup available on the next launch.
- Welcome audio is bundled for offline playback on every platform. No audio is recorded or uploaded.
- The English and Spanish READMEs now play the editor demo inline on GitHub.

Existing layouts and preferences remain compatible. Linux playback uses PulseAudio utilities, already required for system audio capture.
