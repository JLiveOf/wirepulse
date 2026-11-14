## Instalador de Windows e inicio automático opcional

- Nuevo **instalador de Windows x64**, sin permisos de administrador: instala en la cuenta actual, crea el acceso del menú Inicio, ofrece acceso al escritorio y permite desinstalar desde Windows.
- La primera apertura de **Windows, Linux y macOS** pregunta si deseas abrir WirePulse al iniciar sesión. La elección se guarda y puede cambiarse en **Ajustes → Inicio**.
- El inicio automático solo se activa tras elegirlo y abre el fondo sin mostrar los ajustes. En macOS utiliza los ítems de inicio nativos y respeta su estado de aprobación.
- El instalador admite reinstalación/actualización, conserva las preferencias y elimina su entrada de inicio al desinstalar. Pide cerrar WirePulse si está abierto.
- Se mantienen las mejoras de rendimiento de 2.2, los perfiles, las posiciones independientes por pantalla, Cider y las portadas.

### Elige tu descarga

| Sistema | Archivo |
| --- | --- |
| **Windows x64 · recomendado** | **WirePulse-2.3.0-win-x64-Setup.exe** |
| Windows x64 portátil | WirePulse-2.3.0-win-x64.zip |
| Linux Mint / Cinnamon X11 | wirepulse_2.3.0_amd64.deb |
| Linux x64 portátil | WirePulse-2.3.0-linux-x64.tar.gz |
| macOS / Apple silicon | WirePulse-2.3.0-macOS-arm64.zip |

Cierra la versión anterior antes de instalar. El instalador no activa el inicio automático por su cuenta: esa elección se hace en la app. El ZIP portátil sigue disponible.

Windows: instalador sin firma comercial, para Windows 10 2004 o posterior x64. Linux requiere X11. macOS requiere 14.2+ y Apple silicon; firma ad hoc, sin notarización. En macOS la opción puede pedir aprobación en Ítems de inicio.

Las pruebas comprueban instalación, reinstalación, acceso del menú Inicio, ejecución de la app instalada y desinstalación en Windows; conservación de preferencias y limpieza de la entrada de inicio; diálogo de consentimiento y rechazo; registro por usuario de Windows/Linux y servicio nativo simulado de macOS. Las pruebas de macOS no alteran los ítems de inicio reales del equipo de compilación.
