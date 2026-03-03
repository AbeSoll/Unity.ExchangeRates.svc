# Unity Exchange Rates API — Latest Updates Demo Script

> Casual presentation script for code review with line manager and leader.
> Covers: **Security**, **API Versioning**, and **New GET Currencies Endpoint**.

---

## 🎯 What We'll Cover Today

| # | Topic | Status |
|---|-------|--------|
| 1 | [Security — Rate Limiting, CORS, Audit Logging](#1--security) | ✅ Done |
| 2 | [API Versioning — V1 Setup & V2 Migration Guide](#2--api-versioning) | ✅ Done |
| 3 | [New Endpoint — GET Currencies](#3--new-endpoint--get-currencies) | ✅ Done |
| 4 | [Upcoming — CurrencyId Column](#4--upcoming--currencyid) | 🔜 Next |

---

## 1 — Security

> "So for security, we've implemented three layers of protection — Rate Limiting, CORS Lock-down, and Audit Logging. Let me walk through each one."

---

### 1.1 Rate Limiting

> "Rate limiting prevents any single IP from flooding our API. If someone tries to spam hundreds of requests per second, we block them with a 429 status code."

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Program.cs` — `ConfigureRateLimit()` method

```csharp
static void ConfigureRateLimit(IServiceCollection services, IConfiguration configuration)
{
    services.AddMemoryCache();                             // Store counters in RAM
    services.Configure<IpRateLimitOptions>(                // Read rules from appsettings
        configuration.GetSection(nameof(IpRateLimitOptions)));
    services.Configure<IpRateLimitPolicies>(
        configuration.GetSection(nameof(IpRateLimitPolicies)));
    services.AddInMemoryRateLimiting();                    // In-memory counter
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
}
```

> "We're using the `AspNetCoreRateLimit` package. The config is fully driven by appsettings — no need to recompile to change limits."

📂 **Config:** `src/Unity.ExchangeRates.Api/appsettings.Development.json`

```json
"IpRateLimitOptions": {
    "DisableRateLimitHeaders": true,
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "True-Client-IP",
    "HttpStatusCode": 429,
    "GeneralRules": [
        {
            "Endpoint": "*",
            "Period": "1s",
            "Limit": 500
        }
    ],
    "QuotaExceededResponse": {
        "Content": "{{ \"message\": \"Quota exceeded. Please try again later.\" }}",
        "ContentType": "application/json",
        "StatusCode": 429
    }
}
```

> "So here we allow 500 requests per second per IP. That's plenty for normal usage, but it blocks automated abuse. The response is proper JSON — not a blank error page — so the client knows exactly what happened."

**Why this matters:**
- Protects database connection pool from being exhausted
- Prevents denial-of-service from a single source
- Config-driven — can tighten limits in production without redeploying
- Same pattern as Facility API

📂 **Middleware registration:** `src/Unity.ExchangeRates.Api/Program.cs` — Line 59

```csharp
app.UseIpRateLimiting();  // Runs AFTER API Key auth
```

> "Notice the ordering here — rate limiting runs after authentication. This means unauthenticated requests don't count against the quota. Only valid requests get rate-limited."

---

### 1.2 CORS Lock-down

> "CORS controls which domains can call our API from a browser. In dev mode, we allow everything. In production, only listed origins are allowed."

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Program.cs` — `ConfigureCors()` method

```csharp
static void ConfigureCors(IWebHostEnvironment environment, IServiceCollection services, IConfiguration configuration)
{
    var corsOptions = configuration.GetSection(nameof(CorsOptions)).Get<CorsOptions>();

    if (corsOptions == null)
    {
        if (environment.IsDevelopment())
            builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();   // Dev: allow all
        else
            throw new InvalidOperationException("Cors is not configured"); // Prod: MUST configure!
    }
    else
    {
        builder.WithOrigins(corsOptions.Origins)
               .AllowAnyHeader().AllowAnyMethod().AllowCredentials();     // Prod: only listed origins
    }
}
```

> "There's a safety net here — if someone tries to deploy to production without configuring CORS, the app throws an exception and won't start. It's better to fail loudly than to run wide open."

📂 **Config:** `appsettings.Development.json`

```json
"CorsOptions": {
    "Origins": ["#{CORS_ORIGINS}#"]
}
```

> "The `#{CORS_ORIGINS}#` is a CI/CD pipeline placeholder. Azure DevOps replaces it with the actual domain during deployment."

---

### 1.3 Audit Logging

> "Every API request is automatically captured as an audit trail — who called it, when, what they sent, and what we returned. This is for compliance and debugging."

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Configurations/AuditConfigurationBuilderExtensions.cs`

```csharp
public static IApplicationBuilder UseAuditLog(this WebApplication builder)
{
    builder.UseAuditMiddleware(_ => _
        .FilterByRequest(rq => !rq.Path.Value.EndsWith("favicon.ico"))
        .WithEventType("{verb}:{url}")         // e.g. "GET:/api/v1/exchange-rates/usd"
        .IncludeHeaders()                      // Capture request headers
        .IncludeResponseHeaders()              // Capture response headers
        .IncludeRequestBody()                  // Capture JSON body
        .IncludeResponseBody(ctx =>
            ctx.Response.StatusCode != 200));   // Response body ONLY for errors

    builder.Use(async (context, next) => {
        context.Request.EnableBuffering();      // Allow body to be read twice
        await next();
    });

    return builder;
}
```

> "A few things to highlight here:
> - We skip `favicon.ico` — no point logging browser icon requests
> - We capture response body only when it's NOT 200 OK — this saves storage because success data is already in the database
> - `EnableBuffering()` — by default, the request body stream can only be read once. Audit reads it, then the controller reads it — that's twice. Buffering allows this."

**Event-driven architecture — 6 classes across 4 layers:**

| # | Class | Layer | Path | Role |
|---|-------|-------|------|------|
| 1 | `IEvent.cs` | Domain | `Domain/Events/` | Base interface |
| 2 | `IAuditLogEvent.cs` | Domain | `Domain/Events/` | Audit event contract |
| 3 | `AuditLogEvent.cs` | Domain | `Domain/Events/` | Concrete event |
| 4 | `IAuditLogEventDispatcher.cs` | Service | `Service/Services/` | Dispatcher interface |
| 5 | `AuditLogEventHandler.cs` | Service | `Service/EventHandlers/` | Handles event → writes to file |
| 6 | `AuditLogEventDispatcher.cs` | Shared | `Shared/Services/` | Publishes via Mediator |

> "The key design decision here is that business code doesn't know HOW audit logs are stored. Currently it's file-based — but if we need to switch to database storage later, we only change the `DataProvider` config. Nothing else changes."

📂 **Data provider config:** `src/Unity.ExchangeRates.Service/ServiceCollectionExtensions.cs`

```csharp
var auditLogPath = configuration["AppSettings:AuditLogPath"] ?? "audit-logs";
Audit.Core.Configuration.DataProvider = new FileDataProvider(cfg => cfg.Directory(auditLogPath));
```

> "The audit log path is config-driven — reads from `AppSettings:AuditLogPath` in appsettings. For development, logs go to `C:\Logs\ExchangeRates\logs`. For production, CI/CD pipeline sets the path."

📂 **Config:** `appsettings.json`
```json
"AppSettings": {
    "ConfigPath": "#{CONFIG_PATH}#",
    "AuditLogPath": "#{LOG_PATH}#"
}
```

📂 **Config (dev):** `appsettings.Development.json`
```json
"AppSettings": {
    "ConfigPath": "",
    "AuditLogPath": "C:\\Logs\\ExchangeRates\\logs"
}
```

> "Same pattern as Facility — all paths are externalized so each environment can have different log locations."

---

## 2 — API Versioning

> "We implemented URL segment versioning. Every endpoint has the version right in the URL path — `/api/v1/exchange-rates/...`. This is the same approach Facility uses."

---

### 2.1 How It's Configured

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Program.cs` — `ConfigureApiVersioning()` method

```csharp
static void ConfigureApiVersioning(IServiceCollection services)
{
    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;     // Backward compatible
        options.DefaultApiVersion = new ApiVersion(1, 0);       // Default = v1.0
        options.ReportApiVersions = true;                       // Shows in response header
        options.ApiVersionReader = new UrlSegmentApiVersionReader();  // Version from URL
    })
    .AddApiExplorer(setup =>
    {
        setup.GroupNameFormat = "'v'VVV";              // Format: v1, v2
        setup.SubstituteApiVersionInUrl = true;        // Auto-replace {version} in route
    });
}
```

> "Let me break this down:
> - `AssumeDefaultVersionWhenUnspecified` — if an old client calls without a version, it defaults to v1. No breakage.
> - `UrlSegmentApiVersionReader` — version is read from the URL path itself, not a query param or header. Cleaner.
> - `ReportApiVersions` — the response includes an `api-supported-versions` header so clients know what's available."

📂 **Controller:** `src/Unity.ExchangeRates.Api/Controllers/ExchangeRateController.cs`

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/exchange-rates")]
public class ExchangeRateController : BaseApiController
```

> "The controller is tagged as version 1.0. The route template `v{version:apiVersion}` automatically resolves to `v1`."

---

### 2.2 Swagger Integration

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Program.cs` — `ConfigureSwagger()` method

```csharp
foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
{
    swagger.SwaggerDoc(description.GroupName, new OpenApiInfo
    {
        Title = "Unity Exchange Rates API",
        Version = $"{description.ApiVersion}"
    });
}
```

> "This loops through all registered versions and creates a separate Swagger doc for each. When we add V2 later, Swagger automatically shows a dropdown to switch between V1 and V2."

---

### 2.3 How to Add V2 Later (While V1 Keeps Running)

> "The most common question is — what happens when we need V2? How do we keep V1 alive? It's actually straightforward. Let me show the 5-step process."

#### Step 1 — Create a New V2 Controller

📂 **New file:** `Api/Controllers/ExchangeRateV2Controller.cs`

```csharp
[ApiController]
[ApiVersion("2.0")]                                            // Tag as V2
[Route("api/v{version:apiVersion}/exchange-rates")]            // Same route template
public class ExchangeRateV2Controller : BaseApiController
{
    [HttpGet("{currency}")]
    public async Task<IActionResult> GetRate(string currency, [FromQuery] string? date)
    {
        // V2: different response format, additional fields, etc.
        var query = new ExchangeRateV2Query { ... };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
```

> "V1 controller stays EXACTLY as is. We don't touch it at all. We create a brand new controller."

#### Step 2 — Both Versions Run Simultaneously

```
/api/v1/exchange-rates/usd    ← V1 clients still work
/api/v2/exchange-rates/usd    ← V2 clients use the new format
```

> "ASP.NET automatically routes to the correct controller based on the `[ApiVersion]` attribute. No extra configuration needed."

#### Step 3 — Swagger Shows Both

> "Since our `ConfigureSwagger()` loops all versions, the Swagger UI automatically shows a dropdown with `v1` and `v2`. Developers can test both."

#### Step 4 — Deprecate V1

```csharp
[ApiVersion("1.0", Deprecated = true)]   // Add this to V1 controller
```

> "V1 still works — it doesn't break. But the response header shows `api-deprecated-versions: 1.0`, and Swagger marks it as deprecated. This gives clients time to migrate."

#### Step 5 — Remove V1 (Eventually)

> "Only when all clients have migrated to V2, we delete the V1 controller. Not before."

**Folder structure when V2 exists:**

```
Controllers/
├── ExchangeRateController.cs          ← V1 (kept / deprecated)
├── ExchangeRateV2Controller.cs        ← V2 (new)
└── Base/BaseApiController.cs          ← Shared

Service/Mediator/Queries/
├── ExchangeRates/                     ← V1 handlers
├── ExchangeRatesV2/                   ← V2 handlers (if logic differs)
└── Currencies/                        ← Shared across versions
```

| Scenario | What to do |
|----------|-----------|
| Adding V2 | New controller + `[ApiVersion("2.0")]` |
| V1 still active | **Don't touch** V1 controller |
| Deprecating V1 | Add `Deprecated = true` to V1's attribute |
| Removing V1 | Delete V1 controller only after ALL clients migrate |

> "The infrastructure is fully ready. When V2 is needed, it's just adding a new controller and handler — no config changes."

---

## 3 — New Endpoint — GET Currencies

> "We added a new GET endpoint that lists all available currencies from the database. This is useful for clients to know which currencies they can query or sync."

---

### 3.1 The Endpoint

**URL:** `GET /api/v1/exchange-rates/currencies`

**Response:**
```json
{
  "status": "Success",
  "data": [
    { "currencyCode": "usd", "currencyName": "US Dollar" },
    { "currencyCode": "gbp", "currencyName": "British Pound" },
    { "currencyCode": "eur", "currencyName": "Euro" },
    { "currencyCode": "sgd", "currencyName": "Singapore Dollar" }
  ]
}
```

---

### 3.2 Controller

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Controllers/ExchangeRateController.cs`

```csharp
[HttpGet("currencies")]
[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> GetCurrencies()
{
    _logger.LogInformation("GetCurrencies request received");
    var query = new GetCurrenciesQuery();           // No mapper needed — no input params
    var result = await _mediator.Send(query);       // Send to handler via Mediator
    return ApiResponse<BaseResult>(result);          // Return directly — no mapping needed
}
```

> "This follows the exact same CQRS pattern as the other endpoints — create a query, send through Mediator, return the result. The controller is thin — only 4 lines of logic."

---

### 3.3 Query & Handler

📂 **Where to look:** `src/Unity.ExchangeRates.Service/Mediator/Queries/Currencies/GetCurrenciesQuery.cs`

```csharp
public class GetCurrenciesQuery : IRequest<Result<BaseResult>>
{
    // Empty — no parameters needed, fetches all currencies
}
```

> "The query class is empty because we're fetching everything. No filters needed."

📂 **Where to look:** `src/Unity.ExchangeRates.Service/Mediator/Queries/Currencies/GetCurrenciesQueryHandler.cs`

```csharp
public async ValueTask<Result<BaseResult>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
{
    try
    {
        _logger.LogDebug("GetCurrenciesQueryHandler: Fetching all active currencies");

        var currencies = await _repository.GetActiveCurrenciesAsync(cancellationToken);

        _logger.LogInformation("GetCurrenciesQueryHandler: Retrieved {Count} currencies", currencies.Count);

        var result = currencies.Select(c => new
        {
            currencyCode = c.Id,           // CurrencyCode from database
            currencyName = c.CurrencyName
        }).ToList();

        return new BaseResult() { data = result };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "GetCurrenciesQueryHandler: Failed to retrieve currencies");
        return Result.Fail(new GeneralError() { errorCode = "00500", errorMsg = ex.Message });
    }
}
```

> "The handler is straightforward — call the repository, project into a clean anonymous object (just code and name), wrap in BaseResult. Error handling follows the same pattern as our other handlers — catch, log, return a fail result."

**Why this endpoint matters:**
- Clients can discover available currencies dynamically — no hardcoding
- If we add a new currency to the database, the frontend sees it immediately
- Used by the frontend dropdown to populate currency selection

---

### 3.4 File Summary

| File | Path | Role |
|------|------|------|
| `ExchangeRateController.cs` | `Api/Controllers/` | Added `GetCurrencies()` method |
| `GetCurrenciesQuery.cs` | `Service/Mediator/Queries/Currencies/` | Query class (empty — no params) |
| `GetCurrenciesQueryHandler.cs` | `Service/Mediator/Queries/Currencies/` | Fetch from DB, return list |

**Current endpoint inventory:**

| Method | URL | Purpose |
|--------|-----|---------|
| `GET` | `/api/v1/exchange-rates/{currency}?date=yyyy-MM-dd` | Get rate for specific currency |
| `POST` | `/api/v1/exchange-rates/sync` | Sync rates from BNM API |
| `GET` | `/api/v1/exchange-rates/currencies` | **NEW — List all currencies** |

---

## 4 — Upcoming — CurrencyId

> "One thing we're planning next is to add a `CurrencyId` column (auto-increment integer) to the Currency table. Currently the table uses `CurrencyCode` (string) as the primary key. The new structure will have both — `CurrencyId` as PK and `CurrencyCode` as a unique indexed column. This change hasn't been implemented yet — will be done in the next sprint."

**Planned table structure:**
```
Currency
├── CurrencyId (int, PK, auto-increment)   ← NEW
├── CurrencyCode (nvarchar(10), unique)
├── CurrencyName (nvarchar(100))
├── UnitBase (int)
├── CreatedOn, CreatedBy, ModifiedOn, ModifiedBy, IsDeleted
```

---

## 📋 Quick Reference — All Security Layers

```
Request comes in
  → 1. ExceptionHandlerMiddleware     (catches all unhandled exceptions)
  → 2. ApiKeyAuthMiddleware           (validates X-Api-Key header)*
  → 3. IpRateLimiting                 (checks request quota per IP)
  → 4. HTTPS Redirection              (forces HTTPS)
  → 5. CORS                           (checks allowed origins)
  → 6. Audit Logging                  (captures request/response)
  → 7. Controller                     (handles the request)
Response goes out ←
```

*_Note: API Key auth is temporary (Phase 1). Will be replaced by IDP/JWT in Phase 2._
