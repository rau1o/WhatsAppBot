# WhatsApp Bot SaaS — Esquema técnico completo

> Documento de contexto para retomar el proyecto en otra conversación. Describe el estado actual real del sistema (no aspiracional) al momento de escribirlo.

## 1. Qué es el producto

SaaS multi-tenant de automatización de WhatsApp Business para pymes de Bolivia (piloto: una ferretería/tienda de redes en Santa Cruz de la Sierra). Cada tenant (negocio) tiene su propio número de WhatsApp conectado vía **WhatsApp Cloud API oficial de Meta**, un catálogo de productos propio, y un panel de administración web separado del bot.

**Flujo end-to-end del cliente final:**
1. Cliente escribe al WhatsApp del negocio → bot saluda, manda ubicación y foto de fachada.
2. Bot muestra catálogo (lista interactiva de WhatsApp, paginada de a 9 productos).
3. Cliente elige un producto → bot pide que **escriba la cantidad** como texto libre (con validaciones) → se agrega al pedido.
4. Cliente puede seguir agregando productos, ver el pedido acumulado, o finalizar.
5. Al finalizar: bot manda resumen + QR de pago (Cloudflare R2) + pide comprobante.
6. Cliente manda foto del comprobante → bot solo guarda el `WhatsAppMediaId` (nunca descarga la imagen — el staff la revisa directo en la app de WhatsApp Business).
7. Staff aprueba/rechaza desde el panel admin. Al aprobar, el pedido entra a la cola de "preparación".
8. Staff marca el pedido como "Listo para recoger" y luego "Entregado" desde el panel.

**Prioridad de diseño explícita**: minimizar costos de infraestructura (validado con benchmark de mercado — el segmento apunta a Bs bajos/USD 15-50 mensual equivalente).

---

## 2. Stack tecnológico

| Capa | Tecnología |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API, Clean Architecture |
| Panel admin | Blazor Server (.NET 8), proyecto separado del Api |
| Base de datos | PostgreSQL vía **Supabase** (free tier), Session Pooler puerto 5432 (NO Transaction Pooler 6543 — rompe prepared statements de EF) |
| ORM | Entity Framework Core + Npgsql |
| Jobs en background | Hangfire + Hangfire.PostgreSql (también evita que Supabase pause el proyecto por inactividad) |
| Auth Api | ASP.NET Core Identity + JWT Bearer (PBKDF2, 600.000 iteraciones) |
| Auth Panel | JWT guardado en `ProtectedSessionStorage`, scoped al circuito de Blazor Server |
| Storage de archivos | Cloudflare R2 (S3-compatible, sin costo de egress) vía `AWSSDK.S3` |
| Mensajería | WhatsApp Cloud API (Meta) — HTTP directo, sin SDK de terceros |
| Mapa/geocoding (panel) | Leaflet + OpenStreetMap + Nominatim (gratis, sin API key) |
| Hosting | Railway.app (Hobby plan, ~$5/mes por servicio — Api y AdminPanel son dos servicios separados) |
| Tests | xUnit + FluentAssertions, todo en memoria (sin Testcontainers) |

---

## 3. Estructura de la solución

```
WhatsAppBot.sln
├── src/
│   ├── WhatsAppBot.Domain/           # Entidades y enums puros, sin dependencias
│   ├── WhatsAppBot.Application/      # Casos de uso, puertos (interfaces), StateHandlers
│   ├── WhatsAppBot.Infrastructure/   # EF Core, Identity, Hangfire, R2, WhatsApp HTTP sender
│   ├── WhatsAppBot.Api/              # Controllers, Middleware, Program.cs
│   └── WhatsAppBot.AdminPanel/       # Blazor Server — proyecto standalone, SOLO habla con el Api por HTTP
└── tests/
    └── WhatsAppBot.Application.Tests/  # Tests de StateHandlers, con dobles en memoria
```

**Regla de dependencias (Clean Architecture)**: Domain no depende de nada. Application depende solo de Domain (vía puertos/interfaces en `Application/Abstractions`). Infrastructure implementa esos puertos. Api arma todo vía DI. **AdminPanel es una excepción notable: no referencia ningún otro proyecto de la solución** — es un cliente HTTP más de la Api, como lo sería cualquier integración externa futura. Única excepción a esa regla: el AdminPanel tiene su propio `DbContext` mínimo (`DataProtectionKeysDbContext`) apuntando a la misma base de Supabase, exclusivamente para persistir claves de Data Protection (ver sección 9).

---

## 4. Modelo de dominio

### Entidades (`WhatsAppBot.Domain/Entities`)

- **Tenant**: `Id, Name, WhatsAppPhoneNumberId, LocationLatitude/Longitude/Name/Address, FacadePhotoUrl, PaymentQrImageUrl`
- **Conversation**: `Id, TenantId, CustomerPhoneNumber, State (ConversationState), LastMessageAt, PendingProductId (Guid?)`
  - `PendingProductId` guarda qué producto está esperando que el cliente le escriba la cantidad — WhatsApp no tiene un mecanismo nativo para "esperar texto libre atado a un contexto puntual".
- **Product**: `Id, TenantId, Name, Description, Price, ImageUrl, IsActive`
- **Order**: `Id, TenantId, ConversationId, Status (OrderStatus), FulfillmentStatus (OrderFulfillmentStatus?), CreatedAt, Items (List<OrderItem>), Total (computado)`
- **OrderItem**: `Id, OrderId, ProductId, ProductName (snapshot), UnitPrice (snapshot), Quantity`
  - Método de dominio clave: `Order.AddOrIncrementItem(Product, int quantity = 1)` — si el producto ya está en el pedido, suma cantidad; si no, agrega fila nueva.
- **PaymentProof**: `Id, TenantId, OrderId, WhatsAppMediaId, Status (PaymentProofStatus), CreatedAt, ReviewedByUserId, ReviewedAt`
- **AppUser** (vive en Infrastructure/Identity, extiende `IdentityUser<Guid>`): `TenantId, DisplayName`

### Enums

```csharp
enum ConversationState { New, Greeted, BrowsingCatalog, AwaitingQuantity, BuildingOrder, AwaitingPayment, PaymentInReview, Confirmed }

enum OrderStatus { Draft, Submitted, Abandoned }

// Independiente de OrderStatus a propósito: éste es "en qué parte de la
// preparación física está", solo tiene sentido una vez pago el pedido.
enum OrderFulfillmentStatus { Pending, ReadyForPickup, Completed }

enum PaymentProofStatus { PendingReview, Approved, Rejected }
```

**Nota de migraciones**: todos los enums se persisten como `string` (`.HasConversion<string>()`), no como `int`. Esto significa que **agregar un valor nuevo a un enum NO requiere migración de EF** (la columna es `varchar`, no hay `CHECK CONSTRAINT`) — solo hace falta migración cuando cambia la **estructura** (columna nueva, tabla nueva, tipo de dato).

---

## 5. Máquina de estados de la conversación (el corazón del bot)

Cada `IStateHandler` maneja exactamente un `ConversationState`, resuelto vía `StateHandlerResolver` (diccionario armado desde `IEnumerable<IStateHandler>` inyectado por DI).

| Estado | Handler | Qué hace |
|---|---|---|
| `New` | `NewConversationStateHandler` | Saluda, manda ubicación + foto de fachada |
| `Greeted` | *(no tiene handler propio — transición inmediata)* | |
| `BrowsingCatalog` | `CatalogStateHandler` | Muestra catálogo paginado (9 productos + fila "Ver más" si hace falta), maneja "Ver pedido" y "Finalizar pedido" |
| `AwaitingQuantity` | `QuantityInputStateHandler` | Espera que el cliente escriba la cantidad (texto libre, validado), o el botón "Cancelar" |
| `BuildingOrder` | `OrderReviewStateHandler` | Arma resumen, marca `Submitted`, manda QR (con try/catch — un QR roto no debe tumbar el resto del flujo), pide comprobante |
| `AwaitingPayment` | `PaymentProofStateHandler` | Espera la imagen del comprobante (guarda solo el `WhatsAppMediaId`, nunca descarga la imagen) |
| `PaymentInReview` | `PaymentInReviewStateHandler` | Le avisa al cliente que está en revisión si escribe algo mientras tanto |
| `Confirmed` | `ConfirmedStateHandler` | Estado terminal — pedido aprobado y en preparación |

### El patrón `ContinueImmediately` (bug importante que ya se resolvió)

**Problema encontrado**: el diseño original solo invocaba UN handler por mensaje entrante, según el estado *antes* de procesar. Esto significaba que el handler del estado *nuevo* recién corría en el *próximo* mensaje del cliente — ej. tocar "Finalizar pedido" cambiaba el estado a `BuildingOrder` pero no mandaba nada hasta que el cliente escribiera otra vez.

**Solución**: `StateResult` tiene un flag `ContinueImmediately` (default `false`). `MessageProcessor.ProcessAsync` loopea (con tope de seguridad de 5 iteraciones) — si el handler pide `ContinueImmediately: true`, se re-invoca inmediatamente el handler del estado nuevo con un `IncomingMessage` sintético vacío (simulando "recién entré a este estado, nada del cliente todavía").

```csharp
public record StateResult(ConversationState NextState, bool ContinueImmediately = false);
```

Se usa en exactamente 3 lugares (los únicos donde el handler siguiente necesita mostrar contenido que nadie más manda):
- `NewConversationStateHandler` → `BrowsingCatalog` (mostrar catálogo apenas termina el saludo)
- `CatalogStateHandler` (botón Finalizar) → `BuildingOrder` (mostrar resumen+QR apenas se finaliza)
- `OrderReviewStateHandler` (pedido vacío) → `BrowsingCatalog`

**Cuidado**: NO usarlo en transiciones donde el handler ANTERIOR ya mandó su propio mensaje de "entrada" (ej. `OrderReviewStateHandler` → `AwaitingPayment` ya manda "mandá tu comprobante" él mismo — usar el flag ahí generaría un mensaje duplicado/redundante).

### Reset automático y manual

`MessageProcessor` tiene dos mecanismos que corren *antes* de resolver el handler:

1. **Comando de texto** (`reiniciar`, `cancelar`, `reset`, etc. — comparación exacta, no `Contains`, para no disparar por accidente): abandona el pedido activo, resetea a `BrowsingCatalog`, funciona sin importar el estado actual.
2. **Timeout automático** (default 6 horas, configurable vía `ConversationTimeout:StaleAfterHours`): si la conversación lleva más de X horas en un estado "trabado" (`AwaitingQuantity`, `BuildingOrder`, `AwaitingPayment`, `PaymentInReview`) sin actividad, se resetea igual que el comando manual.

Ambos comparten `AbandonOrderAndResetAsync` (marca el `Order.Status = Abandoned`, limpia `PendingProductId`, avisa al cliente).

---

## 6. Multi-tenancy

Vía **global query filter de EF Core** en `WhatsAppBotDbContext`, keyed en `ICurrentTenantAccessor.TenantId` (scoped, seteado explícitamente al principio de cada job/request — `_currentTenant.SetTenant(tenantId)`). Fail-closed: `TenantId` nulo devuelve cero filas, no todas.

`Tenant`, `AppUser` y la tabla técnica `processed_webhook_messages` (deduplicación, ver abajo) **no** tienen filtro — son globales o pre-tenant.

---

## 7. Idempotencia y deduplicación de webhooks

**Problema real encontrado en producción**: Meta puede reentregar el mismo webhook (típicamente si la respuesta tardó de más — ej. conexión "fría" a Supabase tras un rato sin actividad). Sin protección, esto procesaba el mismo mensaje dos veces.

**Solución**: cada mensaje de WhatsApp trae un `message.id` único. Antes de encolar cualquier job, `WhatsAppWebhookController` intenta **insertar** ese ID en una tabla (`processed_webhook_messages`, PK = el message_id). Un segundo intento con el mismo ID choca contra la constraint única → se descarta con `200 OK` sin encolar nada. Se implementa como `IWebhookDeduplicationService` / `EfWebhookDeduplicationService`.

También se redujeron los reintentos automáticos de Hangfire de 10 (default) a 1 (`AutomaticRetryAttribute { Attempts = 1, DelaysInSeconds = new[] { 15 } }`) — ya no hacen falta tan agresivos ahora que hay deduplicación + mensajes honestos de error, y jobs viejos "resucitando" horas después solo generaban confusión.

---

## 8. Gotchas de EF Core encontrados y resueltos (importante si aparecen bugs similares)

1. **`OrderItem` con Guid client-generado + colección de navegación**: al agregar un item nuevo a `order.Items` (lista en memoria) sin `_db.Set<T>().Add()` explícito, EF Core puede clasificarlo mal como `Modified` en vez de `Added` — porque la clave ya tiene un valor "real" y EF no puede saber si es nuevo o existente. Peor: llamar `_db.Entry(x)` en CUALQUIER entidad dispara `DetectChanges()` de **todo el contexto**, así que un `foreach` ingenuo puede hacer que EF clasifique mal el item ANTES de que el código llegue a corregirlo.

   **Fix real** en `EfOrderRepository.SaveAsync`: desactivar `ChangeTracker.AutoDetectChangesEnabled` temporalmente, comparar `order.Items` contra lo que ya estaba trackeado (`ChangeTracker.Entries<OrderItem>()`) para identificar qué es genuinamente nuevo, marcarlo `Added` explícitamente, y recién ahí reactivar el auto-detect antes de `SaveChangesAsync()` (para que los cambios legítimos en items existentes —ej. incrementar cantidad— se sigan detectando bien).

2. **`DbUpdateConcurrencyException` corrompe el `DbContext` para el resto del job**: si se captura la excepción pero no se limpia el estado, cualquier operación posterior en el MISMO `DbContext` (ej. guardar la conversación al final de `MessageProcessor`) puede fallar con un error que no tiene nada que ver. Fix: `_db.ChangeTracker.Clear()` dentro de cualquier `catch (DbUpdateConcurrencyException)`.

3. **Nunca asumir éxito de un `SaveAsync` que puede tragarse errores en silencio**: `IOrderRepository.SaveAsync` devuelve `Task<bool>`, no `Task` — el caller (StateHandler) SIEMPRE chequea el resultado antes de confirmarle algo al cliente. El bug real que esto arregló: el bot mandaba "Agregado ✅" aunque el guardado hubiera fallado silenciosamente.

4. **`Database.EnsureCreated()` no sirve si la base ya existe** (aunque las tablas de TU contexto puntual no existan) — solo chequea si la base de datos en sí existe. Para el `DataProtectionKeysDbContext` del panel (que comparte la base con el Api, ya poblada por migraciones), se usa `ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS ...")` en vez de `EnsureCreated()`.

5. **`STREAMING-AWS4-HMAC-SHA256-PAYLOAD not implemented`** al subir archivos a Cloudflare R2 con `AWSSDK.S3`: R2 no soporta el streaming SigV4 que el SDK de AWS usa por default. Fix en el `PutObjectRequest`: `DisablePayloadSigning = true` y `DisableDefaultChecksumValidation = true`.

6. **Webhook GET de verificación de Meta**: devolver `Content(challenge, "text/plain")`, nunca `Ok(challenge)` (agrega comillas JSON que rompen la validación de firma de Meta).

---

## 9. Data Protection en el panel (Blazor Server + Railway)

**Problema**: el disco de Railway es efímero. Por default, las claves de Data Protection de ASP.NET Core se guardan en el disco local — cada redeploy generaba claves nuevas, y con eso, cualquier sesión guardada vía `ProtectedSessionStorage` quedaba indescifrable. Resultado: **cada redeploy deslogueaba a todos los usuarios del panel en silencio**.

**Fix**: `DataProtectionKeysDbContext` (mínimo, una sola tabla, sin relación con el dominio de negocio) persiste las claves en la misma base de Supabase vía `PersistKeysToDbContext<T>()`. Única excepción documentada a la regla "el panel no toca la base directamente". La tabla se crea con `CREATE TABLE IF NOT EXISTS` directo al arrancar (ver gotcha #4 arriba), no con migraciones de EF.

Connection string en `ConnectionStrings:DataProtection` (mismo valor que usa el Api, variable de entorno `ConnectionStrings__DataProtection` en Railway).

---

## 10. Api — controllers, middleware, y pulido de producción

### Controllers (`src/WhatsAppBot.Api/Controllers`)
- `WhatsAppWebhookController`: GET (verificación), POST (dedup + encola job)
- `AuthController`: login (rate-limited), change-password
- `ConversationsController`, `ProductsController` (CRUD)
- `PaymentProofsController`: lista pendientes, aprobar/rechazar (al aprobar, activa `Order.FulfillmentStatus = Pending`)
- `OrdersController`: `GET /api/orders?status=Pending|ReadyForPickup|Completed`, `POST /{id}/mark-ready`, `POST /{id}/mark-completed` (con chequeo explícito del estado esperado, para evitar carreras entre dos empleados)
- `UsersController` (solo Owner): invitar, cambiar rol, desactivar
- `TenantSettingsController`: datos del tenant, subida de foto de fachada y QR de pago

### Middleware (orden real en `Program.cs`)
```
UseForwardedHeaders()          # Railway termina TLS en su borde
UseMiddleware<CorrelationIdMiddleware>()   # ID único por request, viaja hasta el job de Hangfire
UseExceptionHandler()          # captura todo lo no manejado, devuelve ProblemDetails + correlationId
UseHsts() / DevSeeder (según entorno)
UseHttpsRedirection()
UseCors("AdminPanel")
UseMiddleware<WhatsAppWebhookSignatureMiddleware>()   # valida HMAC-SHA256 del webhook
UseAuthentication()
UseMiddleware<TenantContextMiddleware>()
UseAuthorization()
UseRateLimiter()
UseStaticFiles()                # sirve comprobantes si se usa LocalFileStorage
MapControllers()
MapHealthChecks("/health")      # sin auth, chequea DB
UseHangfireDashboard("/hangfire")  # Basic Auth propio
```

### Correlation ID
`ICorrelationIdAccessor` (mismo patrón que `ICurrentTenantAccessor`) viaja desde el webhook HTTP hasta el job de Hangfire en background (se pasa como parámetro string al encolar, ya que son `DbContext`/scopes distintos). Header `X-Correlation-Id` en cada response.

### Rate limiting
Global: 200 req/min por IP (protege todo por default). Específico: `/api/auth/login` 5 req/min (se suma encima del global, no lo reemplaza).

### Health check
`GET /health` sin auth, chequea conectividad a la base (`AddDbContextCheck<WhatsAppBotDbContext>`).

---

## 11. Panel admin (Blazor Server)

**Páginas**: Login, Products (CRUD), Conversations, PaymentProofs, **Orders** (pestañas: Para preparar / Listos para recoger / Entregados), Users (solo Owner), Settings (datos + selector de ubicación con Leaflet/Nominatim + subida de fotos), ChangePassword.

**Auth**: JWT guardado en `ProtectedSessionStorage`, manejado por `AuthState` (scoped) + `ApiAuthStateProvider`. Hay un esquema Cookie registrado (`AddAuthentication().AddCookie()`) que **nunca se usa para loguear de verdad** — existe solo para que ASP.NET Core sepa a dónde redirigir la primera carga HTTP de una página `[Authorize]` antes de que exista el circuito de Blazor.

**Selector de ubicación**: Leaflet + OpenStreetMap (mapa interactivo) + Nominatim (geocoding, corre del lado del servidor del panel para evitar CORS y cumplir su política de uso — requiere `User-Agent` identificando la app).

---

## 12. Deployment (Railway + Supabase)

- Dos servicios Railway separados: `whatsappbot-api` y `whatsappbot-admin` (o como se hayan nombrado), cada uno ~$5/mes.
- Dockerfiles multi-stage (`sdk:8.0` → `aspnet:8.0`), build context = raíz del repo (necesario por las referencias entre proyectos).
- Supabase free tier, **Session Pooler** (puerto 5432) — no Transaction Pooler (6543, rompe prepared statements de EF) ni Direct Connection.
- Migraciones se corren **localmente** contra Supabase antes de cada deploy que las necesite:
  ```powershell
  $env:WHATSAPPBOT_CONNECTION = "<connection string de Supabase>"
  dotnet ef migrations add NombreDeLaMigracion -p src\WhatsAppBot.Infrastructure -s src\WhatsAppBot.Api
  dotnet ef database update -p src\WhatsAppBot.Infrastructure -s src\WhatsAppBot.Api
  ```
  La variable `WHATSAPPBOT_CONNECTION` la lee específicamente `WhatsAppBotDbContextFactory` (design-time factory), separada de `ConnectionStrings__Default` que usa la app real en Railway.
- **Regla para saber si hace falta migración**: los enums se guardan como `string`, así que agregar valores nuevos a un enum NUNCA requiere migración. Solo hace falta cuando cambia la estructura real (columna/tabla nueva, tipo de dato, nullable).

### Variables de entorno clave (Railway → `whatsappbot-api`)
```
ConnectionStrings__Default=<supabase session pooler>
Jwt__SigningKey=<32+ caracteres>
WhatsAppCloudApi__AccessToken / VerifyToken / AppSecret
HangfireDashboard__Username / Password
Cors__AllowedOrigins__0=<url del panel>
R2Storage__AccountId / AccessKeyId / SecretAccessKey / BucketName / PublicBaseUrl
ConversationTimeout__StaleAfterHours=6   # opcional, default ya es 6
```

### Variables (Railway → `whatsappbot-admin`)
```
Api__BaseUrl=<url pública del Api>
ConnectionStrings__DataProtection=<mismo connection string de Supabase>
```

---

## 13. Convenciones de código a mantener

- Comentarios en español explicando el **por qué**, no el qué (todo el codebase sigue este estilo).
- Cada `catch` de `DbUpdateConcurrencyException` limpia el `ChangeTracker` y devuelve `bool` de éxito — nunca asumir que un guardado funcionó.
- Repos EF filtran automático por tenant vía global query filter — nunca hay que acordarse de agregar `WHERE TenantId = ...` a mano.
- Tests usan repos en memoria (`InMemoryOrderRepository`, etc., en `WhatsAppBot.Infrastructure/Persistence/InMemoryRepositories.cs`) — no hace falta Postgres para correr `dotnet test`.
- Los StateHandlers son "tontos" respecto a WhatsApp: reciben un `IncomingMessage` (POCO con `Text/InteractiveButtonId/ListReplyId/MediaId`), le hablan a `IWhatsAppMessageSender` (puerto), y no conocen la forma real del JSON de Meta — eso vive en `WhatsAppCloudApiSender` (Infrastructure) y en los `Contracts` del webhook (Api).

---

## 14. Pendientes conocidos / no implementado

- Onboarding de tenants nuevos: hoy requiere insert manual por SQL o correr `DevSeeder` local apuntando a producción — no hay UI de "alta de negocio nuevo".
- Roles más granulares (hoy solo `Owner`/`Staff`).
- Paginación de la tabla de productos en el panel (el catálogo de WhatsApp sí está paginado; la tabla del panel admin no, aunque tampoco se rompe con muchos productos, solo se hace larga).
- Tests de integración con Testcontainers (hoy todo es en memoria).
- El panel admin no tiene su propio correlation ID / rate limiting / health check (sí lo tiene el Api).
- Selección múltiple de productos en una sola pantalla del catálogo de WhatsApp: no es posible con mensajes interactivos normales — requeriría WhatsApp Flows (función más grande de Meta, no implementada).

---

## 15. Historial de bugs de producción resueltos (para no repetirlos)

1. Webhook challenge de Meta devuelto con comillas JSON → arreglado con `Content(challenge, "text/plain")`.
2. Lag de un mensaje entre transición de estado y su handler → arreglado con `ContinueImmediately`.
3. `OrderItem` nuevo clasificado como `Modified` en vez de `Added` por EF → arreglado desactivando auto-detect temporalmente.
4. `DbUpdateConcurrencyException` corrompiendo el `DbContext` para el resto del job → `ChangeTracker.Clear()`.
5. Bot confirmando "Agregado" aunque el guardado fallara → `SaveAsync` devuelve `bool`, se chequea siempre.
6. `Order.AddOrIncrementItem` sumando siempre `1` en vez de la cantidad real pasada (bug de una edición manual) → corregido.
7. Webhooks duplicados de Meta procesándose dos veces → deduplicación por `message.id`.
8. R2 rechazando subidas por streaming SigV4 no soportado → `DisablePayloadSigning = true`.
9. `EnsureCreated()` no creaba la tabla de Data Protection porque la base ya existía → `CREATE TABLE IF NOT EXISTS` explícito.
10. Redeploys del panel deslogueando a todos los usuarios → Data Protection persistido en Postgres.
