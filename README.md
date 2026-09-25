# DOCUMENTACIÓN CREDIT PLATFORM

Plataforma web interna de una entidad financiera para gestionar solicitudes de crédito de sus clientes. Los usuarios autenticados registran solicitudes de crédito (nacen en estado `Pendiente`); los analistas de riesgo las evalúan y las **aprueban o rechazan** bajo reglas de negocio: no exceder la capacidad de pago (tope de aprobación **5×** los ingresos), tope de registro **10×**, **una sola solicitud `Pendiente` por cliente** y cliente **activo**. Stack: **ASP.NET Core MVC (.NET 10) + Identity, EF Core + SQLite, Razor Views en español, sesión y caché con Redis, notificaciones en tiempo real (SignalR + PieSocket) y mensajería asíncrona (RabbitMQ en CloudAMQP)**. Despliegue en **Render** (Docker, 1 instancia) con código en **GitHub** (ramas por pregunta + PRs a `main`).

## SERVICIOS UTILIZADOS

| Servicio | Uso en el proyecto | Nota / evidencia |
|---|---|---|
| **Redis Cloud** (base gestionada) | Sesión distribuida (última solicitud visitada → enlace “Ver última solicitud {Monto}” en el layout) + caché de 60 s del listado por usuario (`creditplatf:*`) con invalidación al registrar o cambiar estado | Badge “desde caché” en el listado; claves visibles en Redis Insight |
| **PieSocket** (cluster `free.blr2`, API v3) | Evento `SolicitudEstadoActualizado` (`SolicitudId`, `Estado`, `MotivoRechazo`) al room privado `private-user-{UsuarioId}` vía REST publish; JWT HS256 por `GET /api/realtime/token` | Doble `wss` en DevTools (SignalR + PieSocket); un 2.º cliente no recibe el evento; anónimo al Hub → 401 |
| **CloudAMQP** (plan Lemming, RabbitMQ) | Cola durable `solicitudes.notificaciones`; productor con confirms tras guardar la `Pendiente`; consumidor `BackgroundService` con ACK manual e idempotencia por `MessageId` | Vista “Mis notificaciones”; reenvío con mismo `MessageId` no duplica |
| **Render** (Web Service Free, Docker) | Hosting. URL: `https://credit-platf.onrender.com` *(confirmar tras el deploy)* | Health check `/health`; 1 sola instancia (SQLite + SignalR + consumidor único) |
| **GitHub** | 8 ramas (7 preguntas + deploy) con PRs hacia `main` | `https://github.com/AlessandroCarreno159/credit-platf180626` |

## RAMAS CREADAS (cada una cerrada con PR a `main`)

- **`feature/bootstrap-dominio`** (PR #1): modelos `Cliente` (`Id, UsuarioId, IngresosMensuales, Activo`) y `SolicitudCredito` (`Id, ClienteId, MontoSolicitado, FechaSolicitud, Estado, MotivoRechazo`); restricciones EF (checks `> 0`, índice parcial único 1 `Pendiente`); regla 5× en `Services/SolicitudReglas.cs`; Home visible con conteos/tabla/reglas; seed (rol `Analista` + 3 usuarios + piloto); UI Identity en español; `app.db` y `obj/` fuera del repo.
- **`feature/catalogo-solicitudes`** (PR #2): “Mis solicitudes” del usuario autenticado con filtros (estado, rango de monto, rango de fechas) validados en servidor + vista detalle con control de propietario (404 ajeno).
- **`feature/solicitudes`** (PR #3): formulario `Create` (crea en `Pendiente`); valida autenticado, cliente activo, 1 `Pendiente`, tope 10×; éxito por redirect al detalle (`TempData`), errores en la misma vista (PRG).
- **`feature/sesion-redis`** (PR #4): sesión Redis-backed + caché 60 s (`Services/CacheSolicitudes.cs`) con invalidación; sin fallback (falla claro sin cadena).
- **`feature/panel-analista`** (PR #5): `/Analista` con `[Authorize(Roles="Analista")]`; lista `Pendiente`; aprobar (bloqueo 5×), rechazar (motivo obligatorio), anti-reproceso; `AccessDenied` en español.
- **`feature/websocket-notificaciones`** (PR #6): Hub `/hubs/solicitudes` `[Authorize]` (transporte WebSocket) + PieSocket v3; orden DB → Redis → evento solo al propietario; reconexión con re-sync (`EstadoJson`).
- **`feature/cloudmq-notificaciones`** (PR #7): productor con confirms + consumidor ACK manual + “Mis notificaciones”; endpoint dev `POST /api/mq/reenviar` (solo Development) con mismo `MessageId`.
- **`deploy/render`** (PR #8): `Dockerfile` + `render.yaml` + este README.

## Corrida local

Requisitos: .NET SDK 10.

```powershell
dotnet restore
dotnet ef database update
dotnet user-secrets set "Redis:ConnectionString" "<cadena-redis>"
dotnet run -- --seed
# Usuarios seed (clave Clave123!): piloto@platf.test, cliente1@platf.test,
# cliente2@platf.test, analista@platf.test (rol Analista)
```

Migraciones: `dotnet ef migrations add <Nombre>` + `dotnet ef database update`. En arranque también vale `APLICAR_MIGRACIONES=true` (migra + seed idempotente). Secrets locales con `dotnet user-secrets` (`Redis:*`, `PieSocket:*` — `ClusterId/ApiKey/ApiSecret`, `RabbitMq:*` — `ConnectionString/QueueName/ConsumerEnabled`). Nunca subir credenciales al repo.

## Variables de entorno (Render)

| Variable | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Data Source=/tmp/app.db;Cache=Shared` (Free efímero; ver SQLite) |
| `APLICAR_MIGRACIONES` | `true` |
| `Redis__ConnectionString` | *(secreto Redis Cloud)* |
| `Redis__InstanceName` | `creditplatf:` |
| `RabbitMq__ConnectionString` | *(URI amqps CloudAMQP)* |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` |
| `RabbitMq__ConsumerEnabled` | `true` |
| `PieSocket__ClusterId` / `__ApiKey` / `__ApiSecret` | *(dashboard PieSocket)* |

Arranque: `ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet credit-platf.dll` (`$PORT` expandido por la shell de Render; no asumir expansión dentro de una env var).

## SQLite en Render (plan Free)

**Efímero**: cada deploy/reinicio recrea la DB (`Migrate` + seed idempotente en arranque con `APLICAR_MIGRACIONES=true`). No crear datos “a mano” en producción esperando que duren. Tras cada reinicio hay que **volver a iniciar sesión** (claves DataProtection en memoria; la sesión Redis sí sobrevive). **Con plan de pago** se conserva montando un disco (`mountPath: /var/data`, 1 GB) y `ConnectionStrings__DefaultConnection=Data Source=/var/data/app.db;Cache=Shared`.

## Reenvío manual MQ (P7)

`POST /api/mq/reenviar` (solo Development, autenticado + propietario/Analista) con `{solicitudId, messageId}`: republica con el **mismo** `MessageId`; el consumidor lo confirma sin duplicar si ya fue procesado. No hay outbox: si el publish falla al registrar, la solicitud queda guardada, se loguea y el detalle muestra el aviso.

## Evidencias reproducibles

- **P6**: sesiones de piloto + analista → aprobar/rechazar = cambio + aviso sin reload; 2.º cliente sin evento; DevTools con `wss` SignalR y `wss://free.blr2.piesocket.com`; `POST /hubs/solicitudes/negotiate` anónimo → 401; reconexión recupera vía `EstadoJson`.
- **P7**: `RabbitMq__ConsumerEnabled=false` → registrar → mensaje pendiente en consola CloudAMQP → `true` → cola vacía + 1 notificación → reenviar mismo `MessageId` → sigue 1.
- **P8**: online login, registro, validaciones, panel analista, badge “desde caché”, “última solicitud”, `wss` seguro y notificación MQ.
