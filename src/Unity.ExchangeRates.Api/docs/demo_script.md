# Unity Exchange Rates API — Full Demo Script

> **How to use this script:** Read each section out loud in a casual, confident tone. The 📂 icon tells you which file to open and show on screen. The 🗣️ icon is what you say. The 💡 icon is key points to highlight.

---

## PART 1 — Opening & Big Picture (2 min)

🗣️ *"So today I want to walk you through the Unity Exchange Rates API that I've been building. Let me start with the big picture of what this service does."*

🗣️ *"This API serves one main purpose — it provides exchange rate data for Life Asia. Every day, it automatically fetches the latest currency exchange rates from Bank Negara Malaysia's open API, stores them in our database, and then exposes endpoints for Life Asia to query those rates."*

🗣️ *"There are two main flows:*
1. *An automated daily job that fetches rates at midnight*
2. *A REST API that lets users query stored exchange rates by currency and date"*

🗣️ *"And as part of production readiness, we have also implemented several layers of security and protection — API versioning, API key authentication, rate limiting, audit logging, and CORS control — which I'll walk through in detail."*

💡 **Key point to emphasize:** This is a **fully automated** system. Once deployed, it runs itself. The API is just for users to retrieve the data that's already been synced.

---

## PART 2 — Project Architecture & Folder Structure (3 min)

📂 **Open: Solution Explorer — show all 6 projects**

🗣️ *"I've structured this project using a layered architecture pattern. It's the same pattern we use in the Facility service. Let me go through each layer and what it's responsible for."*

🗣️ *"We have six separate projects, and each one has a clear responsibility:"*

| Layer | Responsibility |
|-------|---------------|
| **Api** | The entry point — controllers, middleware, security config, Swagger |
| **Domain** | Our data models, entities, and event contracts — pure C# classes |
| **Repository** | Only interfaces — defines the contracts for data access |
| **Infrastructure** | The concrete implementations — EF Core, database context, repository code |
| **Service** | Business logic — CQRS handlers, validators, audit event handlers |
| **Shared** | Cross-cutting concerns — Hangfire jobs, HTTP client setup, event dispatchers |

🗣️ *"The key design principle here is the direction of dependency. The inner layers like Domain and Repository have zero dependencies on outer layers. Infrastructure implements the Repository interfaces. Service contains the business logic. And Api ties everything together."*

💡 **If asked "why separate Repository and Infrastructure?":** *"Repository holds only interfaces — it's like a contract. Infrastructure provides the actual implementation. This separation means we can swap out the database or mock it in tests without touching business logic."*

---

## PART 3 — Entry Point: Program.cs (5 min)

📂 **Open:** `Api/Program.cs`

🗣️ *"Let me start from the entry point of the application — Program.cs. This is where everything gets wired up. There's quite a lot going on here now, so let me walk through section by section."*

### Serilog (line 24)

```csharp
ConfigureLog(builder.Host);
```

🗣️ *"First, we configure Serilog for structured logging. This gives us proper log output with timestamps, log levels, and method names."*

### Service Registration (lines 29-31)

```csharp
builder.Services.RegisterServiceModule(builder.Configuration);
builder.Services.RegisterInfrastructureModule(builder.Configuration);
builder.Services.RegisterSharedServiceModule(builder.Configuration);
```

🗣️ *"Here's where the multi-layer pattern comes together. Each layer has its own registration method:*
- *Service module registers the Mediator pipeline, validators, BNM API settings, the Audit.NET file data provider, and `IHttpContextAccessor`*
- *Infrastructure registers EF Core and repositories*
- *Shared registers Hangfire jobs, the HTTP client for BNM API, and the Audit Log Event Dispatcher"*

### CORS Configuration (line 38)

```csharp
ConfigureCors(builder.Environment, builder.Services, builder.Configuration);
```

🗣️ *"CORS — Cross-Origin Resource Sharing — is configured here. In development it allows any origin, but in production it reads specific allowed origins from appsettings. This prevents unauthorized websites from calling our API."*

📂 **Scroll to bottom of file — show `ConfigureCors()` function**

🗣️ *"The function reads `CorsOptions.Origins` from config. If it's not configured and we're NOT in Development, it throws an error — so we'll never accidentally deploy without CORS being locked down."*

### API Versioning (line 44)

```csharp
ConfigureApiVersioning(builder.Services);
```

📂 **Scroll to `ConfigureApiVersioning()` function**

```csharp
static void ConfigureApiVersioning(IServiceCollection services)
{
    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(setup =>
    {
        setup.GroupNameFormat = "'v'VVV";
        setup.SubstituteApiVersionInUrl = true;
    });
}
```

🗣️ *"We use URL segment versioning — so all our endpoints start with `/api/v1/`. This is the same approach Facility uses. We're using the `Asp.Versioning.Mvc` package version 8.1.0 which is the modern replacement for the older `Microsoft.AspNetCore.Mvc.Versioning` package."*

🗣️ *"Key settings here:*
- *`AssumeDefaultVersionWhenUnspecified = true` — so older clients still work*
- *`DefaultApiVersion = 1.0` — our first version*
- *`UrlSegmentApiVersionReader` — reads the version from the URL path*
- *`SubstituteApiVersionInUrl = true` — replaces `{version}` in the route template"*

### Rate Limiting (line 47)

```csharp
ConfigureRateLimit(builder.Services, builder.Configuration);
```

📂 **Scroll to `ConfigureRateLimit()` function**

```csharp
static void ConfigureRateLimit(IServiceCollection services, IConfiguration configuration)
{
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(configuration.GetSection(nameof(IpRateLimitOptions)));
    services.Configure<IpRateLimitPolicies>(configuration.GetSection(nameof(IpRateLimitPolicies)));
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
}
```

🗣️ *"Rate limiting protects our API from abuse — if someone sends too many requests, they get blocked. We use the `AspNetCoreRateLimit` package, same as Facility. The rules come from `IpRateLimitOptions` in appsettings."*

📂 **Open:** `Api/appsettings.Development.json` — scroll to `IpRateLimitOptions`

🗣️ *"Here you can see the config — currently set to 500 requests per second per IP. If exceeded, the client gets a 429 Too Many Requests response with a JSON message saying 'Quota exceeded'."*

### Swagger Configuration (line 50)

📂 **Scroll to `ConfigureSwagger()` function in Program.cs**

🗣️ *"Swagger is configured to support both versioning and API key authentication. There are two important parts."*

🗣️ *"First — it dynamically creates Swagger docs for each API version using `IApiVersionDescriptionProvider`. Right now we have just v1, but when we add v2, Swagger will automatically show both."*

🗣️ *"Second — the security definition. We define an `ApiKey` security scheme that tells Swagger to show an 'Authorize' button. When you click it, you enter your X-Api-Key and all requests from Swagger will include that header."*

### Middleware Pipeline (lines 57-59)

```csharp
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseIpRateLimiting();
```

🗣️ *"The middleware pipeline runs in order:*
1. *First — exception handler catches any crash and returns a clean JSON response*
2. *Second — API key middleware validates the `X-Api-Key` header*
3. *Third — rate limiting checks if the IP has exceeded the request limit"*

🗣️ *"If the API key is invalid, the request is rejected immediately — it never reaches the rate limiter or the controller."*

### Audit Log Middleware (line 79)

```csharp
app.UseAuditLog();
```

🗣️ *"After CORS, we have the Audit Log middleware. This automatically captures every API request and response for audit trail purposes. I'll show the details in the Audit Logs section."*

### Hangfire Job (lines 85-89)

```csharp
RecurringJob.AddOrUpdate<IExchangeRateSyncJob>(
    "daily-exchange-rate-sync",
    job => job.SyncDailyAsync(CancellationToken.None),
    "0 0 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
```

🗣️ *"This is the Hangfire recurring job. Cron expression `0 0 * * *` = every day at midnight. It runs every single day including weekends — the weekend logic is handled in the business layer."*

---

## PART 4 — API Key Authentication (3 min)

📂 **Open:** `Api/Middlewares/ApiKeyAuthMiddleware.cs`

🗣️ *"This is our API key security layer. Every request goes through this middleware before reaching any controller."*

**Point to the class structure:**

🗣️ *"The middleware is injected with three things:*
- *`RequestDelegate` — to pass the request to the next middleware*
- *`IConfiguration` — to read the valid API key from config*
- *`ILogger` — to log any unauthorized attempts"*

**Point to the `Invoke` method:**

```csharp
public async Task Invoke(HttpContext context)
{
    var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

    // Skip authentication for Swagger and Hangfire (dev tools)
    if (path.StartsWith("/swagger") || path.StartsWith("/hangfire"))
    {
        await _next(context);
        return;
    }
```

🗣️ *"First, it checks the path. Swagger and Hangfire dashboard are excluded from authentication — these are development tools that need to be accessible without a key."*

**Point to the key validation:**

```csharp
    if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
    {
        _logger.LogWarning("API Key missing from request. Path={Path}, IP={IP}",
            context.Request.Path, context.Connection.RemoteIpAddress);
        await WriteUnauthorizedResponse(context, "API Key is required.");
        return;
    }

    var configuredApiKey = _configuration["ApiSecurity:ApiKey"];
    if (!string.Equals(extractedApiKey, configuredApiKey))
    {
        _logger.LogWarning("Invalid API Key provided. Path={Path}, IP={IP}",
            context.Request.Path, context.Connection.RemoteIpAddress);
        await WriteUnauthorizedResponse(context, "Invalid API Key.");
        return;
    }
```

🗣️ *"Then it checks for the `X-Api-Key` header. If missing — 401 Unauthorized. If the key doesn't match what's configured — 401 Unauthorized. Every unauthorized attempt is logged with the request path and the client's IP address — this is important for security monitoring."*

**Point to `WriteUnauthorizedResponse`:**

🗣️ *"The 401 response is returned as proper JSON — not just a status code. It has a status, error code, error message, and timestamp. This is consistent with our other error responses."*

📂 **Open:** `Api/appsettings.Development.json` — point to `ApiSecurity` section

```json
"ApiSecurity": {
    "ApiKey": "dev-unity-exchangerates-key-2026"
}
```

🗣️ *"The API key is stored in appsettings for development. In production, this would come from the IDP (Identity Provider) or Azure Key Vault — not hardcoded in config files."*

💡 **Key point:** *"This is Phase 1 security. When IDP registration is ready, we will add JWT token authentication as Phase 2 — the middleware architecture makes it easy to add more security layers."*

---

## PART 5 — Audit Logs (3 min)

🗣️ *"Let me walk through how audit logging works. This follows the same pattern as Facility, using the Audit.NET library."*

📂 **Open:** `Api/Configurations/AuditConfigurationBuilderExtensions.cs`

```csharp
builder.UseAuditMiddleware(_ => _
    .FilterByRequest(rq => !rq.Path.Value.EndsWith("favicon.ico"))
    .WithEventType("{verb}:{url}")
    .IncludeHeaders()
    .IncludeResponseHeaders()
    .IncludeRequestBody()
    .IncludeResponseBody(ctx => ctx.Response.StatusCode != 200));
```

🗣️ *"This configures the audit middleware to capture every HTTP request — the verb, URL, headers, request body, and response body (only for non-200 responses). It filters out favicon requests since those aren't relevant."*

🗣️ *"The `EnableBuffering()` call at the bottom is important — it allows the request body to be read multiple times, once by the audit middleware and once by the controller."*

📂 **Open:** `Domain/Events/IAuditLogEvent.cs`

🗣️ *"The audit system follows the event-driven pattern:"*

| File | Layer | Purpose |
|------|-------|---------|
| `Domain/Events/IEvent.cs` | Domain | Base event interface extending Mediator's `INotification` |
| `Domain/Events/IAuditLogEvent.cs` | Domain | Audit event contract — EventType, ReferenceId, Message, Data |
| `Domain/Events/AuditLogEvent.cs` | Domain | Concrete event class |
| `Service/Services/IAuditLogEventDispatcher.cs` | Service | Dispatcher interface |
| `Service/EventHandlers/AuditLogEventHandler.cs` | Service | Creates `AuditScope` with IP address and custom fields |
| `Shared/Services/AuditLogEventDispatcher.cs` | Shared | Publishes events via Mediator |

📂 **Open:** `Service/EventHandlers/AuditLogEventHandler.cs`

```csharp
using (var audit = await AuditScope.CreateAsync(notification.EventType, () => notification.Data))
{
    audit.SetCustomField("ReferenceId", notification.ReferenceId);
    audit.SetCustomField("IpAddress", _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());
    audit.Comment(notification.Message);
}
```

🗣️ *"When an audit event is dispatched, this handler creates an AuditScope — which writes to the file data provider. Each audit entry captures the event type, the data, a reference ID, and the client's IP address. The logs are written to the `audit-logs/` folder."*

📂 **Open:** `Service/ServiceCollectionExtensions.cs` — point to Audit config

```csharp
Audit.Core.Configuration.DataProvider = new FileDataProvider(cfg => cfg.Directory("audit-logs"));
services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
```

🗣️ *"The data provider is configured to write audit logs as JSON files in the `audit-logs` directory. `IHttpContextAccessor` is registered so the handler can access the current HTTP request — specifically the client IP."*

💡 **Key point:** *"This is the same pattern Facility uses. In production, we can switch from file-based to database-based audit logs by changing the data provider."*

---

## PART 6 — Domain Models (2 min)

📂 **Open:** `Domain/Models/BaseEntity.cs`

🗣️ *"Let me quickly show the domain models. We have a BaseEntity that all our entities inherit from. It provides common audit fields — `Id`, `CreatedOn`, `CreatedBy`, `ModifiedOn`, `ModifiedBy`, and `IsDeleted`."*

📂 **Open:** `Domain/Models/Currency.cs`

🗣️ *"The Currency model represents currencies we track — like USD, GBP, SGD. It has a `CurrencyCode` as the primary key, a `CurrencyName`, and `UnitBase`. These are pre-populated in the database."*

📂 **Open:** `Domain/Models/ExchangeRateHistory.cs`

🗣️ *"This is the main table — ExchangeRateHistory. Every sync creates a row here with `CurrencyCode`, `RateDate`, `BuyingRate`, `SellingRate`, `MiddleRate`, and `EffectiveDate`."*

💡 **Key point:** *"The difference between `RateDate` and `CreatedOn` is important. `RateDate` is BNM's date. `CreatedOn` is when we stored it. Users query by `CreatedOn`."*

---

## PART 7 — The CQRS Pattern (2 min)

🗣️ *"Before I show the controllers, let me briefly explain the pattern we use — CQRS, which stands for Command Query Responsibility Segregation."*

🗣️ *"The idea is simple — we separate read operations from write operations:*
- *Queries = reading data (GET endpoint)*
- *Commands = writing data / triggering actions (POST sync endpoint)"*

🗣️ *"We use the Mediator library to implement this. The controller creates a query or command and sends it through Mediator. The Mediator finds the right handler and executes it. Each handler is a small, focused class that does one thing."*

---

## PART 8 — Controller (3 min)

📂 **Open:** `Api/Controllers/ExchangeRateController.cs`

🗣️ *"Here's our controller — it's intentionally thin. Notice the important attributes at the top."*

**Point to the class attributes:**

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/exchangerates")]
```

🗣️ *"Three attributes:*
- *`ApiController` — enables automatic model validation and binding*
- *`ApiVersion("1.0")` — this controller belongs to API version 1.0*
- *The route includes `v{version:apiVersion}` — so the actual URL becomes `/api/v1/exchangerates`"*

**Point to the constructor:**

🗣️ *"The controller gets three dependencies injected — AutoMapper for mapping objects, Mediator for sending commands and queries, and a logger that we actively use for request logging."*

**Point to GetRate method:**

```csharp
[HttpGet("{currency}/{date}")]
public async Task<IActionResult> GetRate(string currency, string date)
{
    _logger.LogInformation("GetRate request received: currency={currency}, date={date}", currency, date);
```

🗣️ *"The GET endpoint takes currency and date from the URL. Notice we log every incoming request — this is important for monitoring and debugging. Then it maps the input to a Query, sends through Mediator, returns the result."*

**Point to Sync method:**

```csharp
[HttpPost("sync")]
public async Task<IActionResult> Sync([FromBody] ExchangeRateSyncRequest syncRequest)
{
    _logger.LogInformation("Sync request received: date={date}, session={session}", syncRequest.date, syncRequest.session);
```

🗣️ *"Same pattern for POST — log the request, map to Command, send through Mediator. The controller has zero business logic — it's just a bridge between HTTP and our service layer."*

---

## PART 9 — AutoMapper Profiles (1 min)

📂 **Open:** `Api/Configurations/InitialMapper.cs`

🗣️ *"AutoMapper handles the mapping between request/response objects. Three mappings with matching property names — AutoMapper does the rest automatically."*

---

## PART 10 — Validation Pipeline (2 min)

📂 **Open:** `Service/Behaviors/RequestValidationBehavior.cs`

🗣️ *"Before any command or query reaches its handler, it goes through our validation pipeline. This is a Mediator pipeline behavior — like middleware for Mediator. If validation fails, it short-circuits and returns an error."*

📂 **Open:** `Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandValidator.cs`

🗣️ *"Here's a concrete validator — date must be in `yyyy-MM-dd` format, and session must be one of: 0900, 1130, 1200, or 1700."*

---

## PART 11 — Query Handler (GET flow) (3 min)

📂 **Open:** `Service/Mediator/Queries/ExchangeRates/ExchangeRateQueryHandler.cs`

🗣️ *"This handles what happens when a user queries for a rate. It parses the date, calls the repository to find the rate by `CreatedOn.Date`, and returns the result. If no rate is found, it returns 404 with a clear message."*

💡 **Key point:** *"Users query with today's date. Today's data was synced at midnight, so it should always be available."*

---

## PART 12 — Command Handler (POST / Sync flow) — core business logic (5 min)

📂 **Open:** `Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandHandler.cs`

🗣️ *"This is the heart of the application. Let me walk through it step by step."*

**Point to date resolution:**

```csharp
var targetDate = ResolveBusinessDate(inputDate);
```

📂 **Scroll to `ResolveBusinessDate`:**

```csharp
private static DateTime ResolveBusinessDate(DateTime date)
{
    while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        date = date.AddDays(-1);
    return date;
}
```

🗣️ *"BNM doesn't publish on weekends, so Saturday/Sunday resolve to Friday. Monday also gets Friday's rate because the Hangfire job sends yesterday's date (Sunday) which resolves to Friday."*

**Point to the loop:**

🗣️ *"For each currency: build the BNM API URL → call the API → if error, log and skip → if success, create an ExchangeRateHistory entity → add to database context. All wrapped in a transaction — if anything fails, everything rolls back."*

---

## PART 13 — Hangfire Job (2 min)

📂 **Open:** `Shared/Jobs/ExchangeRateSyncJob.cs`

🗣️ *"The Hangfire job runs at midnight — takes yesterday's date and sends it through Mediator."*

| Job runs at | Yesterday (-1 day) | ResolveBusinessDate | Rate fetched |
|---|---|---|---|
| **Tuesday 12AM** | Monday | Monday ✅ | Monday 1700 |
| **Saturday 12AM** | Friday | Friday ✅ | Friday 1700 |
| **Sunday 12AM** | Saturday → | **Friday** | Friday 1700 |
| **Monday 12AM** | Sunday → | **Friday** | Friday 1700 |

---

## PART 14 — Repository & Database Layer (3 min)

📂 **Open:** `Repository/IExchangeRateRepository.cs` → then `Infrastructure/Repositories/ExchangeRateRepository.cs`

🗣️ *"Repository defines three interface operations. Infrastructure provides the EF Core implementation."*

📂 **Open:** `Infrastructure/UnitOfWork.cs`

🗣️ *"Unit of Work wraps the repository with transaction management — BeginTransaction, Commit, Rollback."*

📂 **Open:** `Infrastructure/Interceptors/EntitySaveChangeInterceptor.cs`

🗣️ *"The SaveChanges interceptor automatically sets `CreatedOn` and `ModifiedOn` — so developers never forget to populate audit fields."*

---

## PART 15 — HTTP Client & Resilience (2 min)

📂 **Open:** `Shared/ServiceCollectionExtensions.cs`

🗣️ *"BNM API client uses HTTP client factory pattern with Polly retry policy — 3 retries with increasing delays (1s, 2s, 5s). This handles transient BNM API failures gracefully."*

🗣️ *"Also notice the `AuditLogEventDispatcher` is registered here as scoped — it connects the audit event system to Mediator."*

---

## PART 16 — Middleware & Error Handling (2 min)

📂 **Open:** `Api/Middlewares/ExceptionHandlerMiddleware.cs`

🗣️ *"Our global exception middleware categorizes errors:*
- *Domain exceptions → 400, logged as Warning*
- *Validation exceptions → 400, logged as Warning*
- *Everything else → 500, logged as Error*

*Always returns proper JSON — never an ugly stack trace."*

---

## PART 17 — Logging Strategy (2 min)

| Level | When we use it | Example |
|-------|---------------|---------|
| **Debug** | Internal ops, dev detail | *"Repository: GetActiveCurrenciesAsync called"* |
| **Information** | Business milestones | *"Sync completed. Synced 8/8 currencies"* |
| **Warning** | Recoverable issues + **unauthorized API access** | *"API Key missing, IP=192.168.1.1"* |
| **Error** | Operation failures | *"BNM API returned 404 for JPY"* |
| **Critical** | System crashes | *"Hangfire job crashed unexpectedly"* |

🗣️ *"Notice that the API Key middleware logs unauthorized attempts at Warning level — so in production we can monitor for suspicious access patterns."*

---

## PART 18 — LIVE DEMO (5 min)

### Demo 1: Swagger — Versioned API + Security

🗣️ *"Let me show you the API running."*

1. **Open Swagger** — point out the dropdown showing **V1**
2. **Show endpoints** — both now under `/api/v1/exchangerates/...`
3. **Click 🔒 Authorize** button → show the ApiKey security scheme
4. **Try WITHOUT key** — GET `/api/v1/exchangerates/usd/2026-02-27` → show **401 Unauthorized** JSON response
5. **Enter key** `dev-unity-exchangerates-key-2026` → Authorize → Close
6. **Try WITH key** — same GET → show **200 OK** with exchange rate data

### Demo 2: POST sync (manual)

1. POST body:
```json
{
  "date": "2026-02-25",
  "session": "1700"
}
```
2. Show response: *"Synced 8 of 8 currencies"*

### Demo 3: Hangfire Dashboard

1. Open `/hangfire` — show `daily-exchange-rate-sync` recurring job
2. Show recent Succeeded jobs

### Demo 4: Database

1. Show **Currency** table — currencies being tracked
2. Show **ExchangeRateHistory** — weekend rows with same `RateDate` but different `CreatedOn`

### Demo 5: Audit Logs

1. Open `audit-logs/` folder after making a request
2. Show the JSON audit file — contains request headers, URL, method, response status

---

## PART 19 — Summary & Close (1 min)

🗣️ *"So to wrap up — the Unity Exchange Rates API has:"*

1. ✅ *"**Fully automated** — Hangfire syncs rates daily at midnight"*
2. ✅ *"**Secured** — API key authentication on every request"*
3. ✅ *"**Versioned** — URL segment versioning (`/api/v1/`)"*
4. ✅ *"**Rate limited** — 500 req/s per IP, 429 if exceeded"*
5. ✅ *"**Audited** — every request/response captured for audit trail"*
6. ✅ *"**CORS controlled** — config-driven allowed origins"*
7. ✅ *"**Resilient** — retry policy for BNM API, transaction rollback"*
8. ✅ *"**Clean architecture** — 6 layers with clear separation"*
9. ✅ *"**CQRS pattern** — commands and queries separated with validation"*
10. ✅ *"**Structured logging** — proper log levels for monitoring"*
11. ✅ *"**Weekend-aware** — automatically resolves to Friday's rate"*
12. ✅ *"**Production-ready** — for Life Asia deployment"*

🗣️ *"Any questions?"*

---

## ⚡ Quick Q&A Cheat Sheet

| Potential question | Answer |
|---|---|
| **Why not call BNM API in real-time when user queries?** | Performance and reliability. We cache in our DB so queries are instant. If BNM is down, our data is still available. |
| **What if the job fails?** | We have the POST endpoint for manual re-sync. Logs will show exactly what failed. |
| **Why duplicate data on weekends?** | Requirement — each day needs a record. Life Asia expects a rate for every date. |
| **Why API key and not JWT?** | Phase 1 — API key gives us immediate protection. JWT will be added when IDP registration is ready. The middleware architecture makes it easy to layer both. |
| **How is the API key stored in production?** | Via IDP (Identity Provider) or Azure Key Vault — not hardcoded in config files. |
| **What happens if rate limit is exceeded?** | Client gets 429 Too Many Requests with a JSON message. Currently set to 500/sec per IP. |
| **How is audit logging stored?** | File-based for now (same as Facility). Can switch to database-based by changing the data provider. |
| **Why CQRS and not just simple services?** | Separation of concerns, easier to test, each handler is focused. Also aligns with Facility's architecture. |
| **What about CORS in production?** | `CorsOptions.Origins` in appsettings is replaced by CI/CD pipeline with actual allowed domains. |
| **What NuGet packages were added for security?** | `Asp.Versioning.Mvc` 8.1.0, `AspNetCoreRateLimit` 5.0.0, `Audit.NET` 21.0.0, `Audit.WebApi.Core` 21.0.0 — all same or similar to what Facility uses. |
