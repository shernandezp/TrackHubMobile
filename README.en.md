# TrackHub Mobile

[← Back to the landing page](README.md) · [Español](README.es.md)

TrackHub Mobile is the .NET MAUI application for drivers and operators.

> **Status: work in progress.** The project builds and authenticates, but the feature set is still being defined. Expect breaking changes.

---

## Overview

The application authenticates against the TrackHub AuthorityServer using the Authorization Code Flow with PKCE, and consumes the Manager and Router GraphQL APIs.

Two OAuth clients are relevant:

| Client | Scope | Principal type |
|---|---|---|
| `mobile_client` | `mobile_scope` | `User` — an operator using the platform from a phone |
| `driver_mobile_client` | `driver_mobile_scope` | `Driver` — a driver authenticated against `security.driver_credentials` |

---

## Quick start

### Prerequisites

- .NET 10 SDK with the MAUI workloads installed (`dotnet workload install maui`)
- A running TrackHub AuthorityServer and backend stack
- Android SDK and/or Xcode, depending on the target platform

### Steps

1. **Clone**

   ```bash
   git clone https://github.com/shernandezp/TrackHubMobile.git
   cd TrackHubMobile
   ```

2. **Point the application at your backend** — the authority and API base URLs live in the application configuration.

3. **Build and run** for your target framework:

   ```bash
   dotnet build -t:Run -f net10.0-android
   ```

---

## Project-specific notes

- **Driver principals must be named explicitly.** `AuthorizeAttribute.PrincipalTypes` defaults to `"User,ServiceClient"`, so a backend request a driver needs to reach must carry `PrincipalTypes = "…,Driver"`. An attribute that does not set the property is **not** unrestricted.
- **Driver-facing capabilities are gated by the `driver-mobile` account feature**, which is checked server-side. A tenant without it will see `FEATURE_DISABLED`.
- **Devices are bound** through `security.driver_device_registrations`.
- The application talks to the same GraphQL surfaces as the web portal; **contract changes on the backend affect it too**, but it is not covered by the portal's codegen drift check.

---

## Documentation

- **Technical** — the [TrackHub wiki](https://github.com/shernandezp/TrackHub/wiki): [Security and Identity](https://github.com/shernandezp/TrackHub/wiki/Security-and-Identity), [Architecture](https://github.com/shernandezp/TrackHub/wiki/Architecture), [Technology](https://github.com/shernandezp/TrackHub/wiki/Technology)
- **User** — in the web portal: the Help button or **F1** on any screen
- **Deployment** — [TrackHub.Deployment](https://github.com/shernandezp/TrackHub.Deployment)

---

## License

Apache License 2.0. See the [LICENSE file](https://www.apache.org/licenses/LICENSE-2.0) for more information.
