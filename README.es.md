# TrackHub Mobile

[← Volver a la página principal](README.md) · [English](README.en.md)

TrackHub Mobile es la aplicación .NET MAUI para conductores y operadores.

> **Estado: trabajo en progreso.** El proyecto compila y autentica, pero el conjunto de funcionalidades todavía se está definiendo. Se deben esperar cambios que rompan compatibilidad (breaking changes).

---

## Descripción general

La aplicación se autentica contra el AuthorityServer de TrackHub usando el Authorization Code Flow con PKCE, y consume las API de Management, Router y Trip Management.

Hay dos clientes OAuth relevantes:

| Client | Scope | Principal type |
|---|---|---|
| `mobile_client` | `mobile_scope` | `User` — un operador que usa la plataforma desde un teléfono |
| `driver_mobile_client` | `driver_mobile_scope` | `Driver` — un conductor autenticado contra `security.driver_credentials` |

---

## Inicio rápido

### Requisitos previos

- .NET 10 SDK con los workloads de MAUI instalados (`dotnet workload install maui`)
- Un AuthorityServer de TrackHub y el stack de backend en ejecución
- Android SDK y/o Xcode, según la plataforma de destino

### Pasos

1. **Clonar**

   ```bash
   git clone https://github.com/shernandezp/TrackHubMobile.git
   cd TrackHubMobile
   ```

2. **Apuntar la aplicación al backend** — las URLs base de la autoridad (authority) y de la API viven en la configuración de la aplicación.

3. **Compilar y ejecutar** para el framework de destino:

   ```bash
   dotnet build -t:Run -f net10.0-android
   ```

---

## Notas específicas del proyecto

- **Los principals de tipo driver deben nombrarse explícitamente.** `AuthorizeAttribute.PrincipalTypes` tiene por defecto `"User,ServiceClient"`, por lo que una solicitud de backend que un driver necesite alcanzar debe llevar `PrincipalTypes = "…,Driver"`. Un atributo que no establece la propiedad **no** está sin restricciones.
- **Las capacidades orientadas al conductor están controladas por la feature de cuenta `driver-mobile`**, que se verifica del lado del servidor. Un tenant que no la tenga verá `FEATURE_DISABLED`.
- **Los dispositivos se vinculan** mediante `security.driver_device_registrations`.
- La aplicación se comunica con las mismas superficies GraphQL que el portal web; **los cambios de contrato en el backend también la afectan**, pero no está cubierta por la verificación de deriva (drift) del codegen del portal.

---

## Documentación

- **Técnica** — la [wiki de TrackHub](https://github.com/shernandezp/TrackHub/wiki): [Security and Identity](https://github.com/shernandezp/TrackHub/wiki/Security-and-Identity), [Architecture](https://github.com/shernandezp/TrackHub/wiki/Architecture), [Technology](https://github.com/shernandezp/TrackHub/wiki/Technology)
- **Usuario** — en el portal web: el botón de Ayuda o **F1** en cualquier pantalla
- **Despliegue** — [TrackHub.Deployment](https://github.com/shernandezp/TrackHub.Deployment)

---

## Licencia

Licencia Apache 2.0. Consulte el [archivo LICENSE](https://www.apache.org/licenses/LICENSE-2.0) para más información.
