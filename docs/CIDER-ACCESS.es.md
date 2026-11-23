# El token de Cider y WirePulse

El `apptoken` autoriza a una aplicación a utilizar la API local de Cider, normalmente en `http://localhost:10767`. No es la contraseña del Apple ID. Se envía como cabecera `apptoken`, sin `Bearer`.

## Qué permite

En la API v2, cada token tiene los permisos que concediste:

- **Playback:** leer/controlar reproducción, pausa, posición, siguiente, repetición y aleatorio.
- **Queue:** consultar y modificar la cola.
- **Library:** consultar biblioteca y editar listas y valoraciones.
- **Audio:** volumen, crossfade, Automix, modos de audio y captura gestionada por Cider.
- **Account:** acceder a tokens de Apple Music y la tienda; es un permiso sensible.
- **Lyrics:** consultar letras y traducciones.
- **Config:** ajustes de solo lectura, con credenciales ocultas.
- **Plugins:** solicitar instalaciones, con confirmación adicional del usuario.

Los tokens anteriores al sistema de permisos pueden tener acceso a toda la API. Los endpoints v1 son antiguos y no separan estos permisos igual que v2. Un token Playback no equivale a una autorización de solo lectura: también puede controlar el reproductor.

Fuentes oficiales: [API v2 y permisos](https://taproom.cider.sh/docs/api/), [API v1 heredada](https://taproom.cider.sh/faq?article=playback-api).

## Qué hace WirePulse ahora

El código actual consulta mediante GET `/api/v2/playback`, con alternativas `/api/v1/playback/now-playing` e `/api/v1/playback/is-playing`. Lee título, artista, álbum, portada, estado, progreso y BPM si Cider los proporciona. No llama a comandos de reproducción, biblioteca, Account, plugins ni captura de Cider. El análisis del sonido usa el sistema de captura propio de WirePulse, independientemente del token.

macOS guarda el token en el Llavero y rechaza redirecciones HTTP. Windows y Linux actualmente lo guardan en el JSON de configuración; Linux restringe el archivo al usuario. No se necesita compartir el token para explicar o mejorar la integración.

## Mejoras propuestas para una tarea posterior

Migrar completamente a v2, pedir solo los permisos necesarios, ofrecer conexión autorizada desde Cider en vez de copiar claves, guardar credenciales de Windows/Linux en almacenes seguros y mostrar diagnósticos claros de permisos y conexión. Los controles de reproducción y las funciones de Audio deben ser opciones explícitas, no privilegios añadidos por defecto.
