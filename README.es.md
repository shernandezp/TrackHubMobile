# TrackHub Mobile

[English](README.md) · [Español](README.es.md)

<img src="https://raw.githubusercontent.com/shernandezp/TrackHub/main/TrackHub.AuthorityServer/src/Web/wwwroot/images/logo.png" alt="TrackHub" width="120" />

La app móvil para los usuarios de cuenta de [TrackHub](https://github.com/shernandezp/TrackHub),
construida con .NET MAUI (Blazor Hybrid) para Android, iOS y Mac Catalyst: el tablero de la flota,
el mapa en vivo, la lista y el detalle de transportadores, y el historial de posiciones leído desde
TrackHub o directamente desde el proveedor GPS.

> **Trabajo en progreso.** Espere cambios que rompan compatibilidad.

Inicia sesión contra el AuthorityServer de TrackHub como `mobile_client` (`mobile_scope`) mediante
una página de inicio de sesión propia de la app — OAuth 2.0 password grant más refresh tokens — y
llama a las API GraphQL de Router y Manager.

## Estructura

| Proyecto | Contenido |
|---|---|
| `TrackHubMobile` | El host MAUI: cabeceras de plataforma, recursos, arranque de la app |
| `TrackHubMobile.MauiLib` | Páginas, view models, servicios y los clientes GraphQL (`net10.0`) |
| `tests/TrackHubMobile.Tests` | Pruebas NUnit sobre `TrackHubMobile.MauiLib` |

## Compilar, ejecutar, probar

Requiere el SDK de .NET 10 con el workload de MAUI (`dotnet workload install maui`), además del SDK
de Android, o Xcode en macOS para iOS y Mac Catalyst.

```bash
git clone https://github.com/shernandezp/TrackHubMobile.git
cd TrackHubMobile

dotnet test tests/TrackHubMobile.Tests
dotnet build TrackHubMobile/TrackHubMobile.csproj -t:Run -f net10.0-android
```

Las URL del servidor, el cliente OAuth y el esquema de callback (`trackhubmobile://`) son constantes
en `TrackHubMobile.MauiLib/Utils/Constants.cs`; apúntelas a su despliegue antes de compilar.

## Notas

- **El servidor debe permitir el password grant para `mobile_client`** — `"passwordGrant": true` en
  ese cliente dentro del `clients.json` del despliegue. Sin él, el inicio de sesión responde
  `unauthorized_client`.
- `NuGet.config` agrega el feed nightly de MAUI junto a nuget.org.
- La app usa las mismas superficies GraphQL que el portal web, así que los cambios de contrato del
  backend también la afectan — pero no está cubierta por la verificación de desviación del codegen
  del portal.

## Documentación

- **Técnica** — la [wiki de TrackHub](https://github.com/shernandezp/TrackHub/wiki):
  [Security and Identity](https://github.com/shernandezp/TrackHub/wiki/Security-and-Identity),
  [Architecture](https://github.com/shernandezp/TrackHub/wiki/Architecture)
- **Despliegue** — [TrackHub.Deployment](https://github.com/shernandezp/TrackHub/tree/main/TrackHub.Deployment)
- **Usuario** — en el portal web: el botón de Ayuda o **F1** en cualquier pantalla

## Licencia

Licencia Apache 2.0 — vea [LICENSE.txt](LICENSE.txt).
