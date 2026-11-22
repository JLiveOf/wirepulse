# WirePulse · macOS

[Español](README.es.md) · [Features and downloads](../README.md)

Requires **macOS 14.2+, Apple silicon** and Xcode / compatible Command Line Tools.

```sh
./build.sh
open dist/WirePulse.app
```

Allow system audio capture when macOS asks. Music artwork may also need Automation permission. Settings → Startup controls login startup, language and stable/beta updates.

The build uses AppKit, SwiftUI, Metal and Core Audio. The script applies an ad-hoc signature; distribution builds are not notarized.
