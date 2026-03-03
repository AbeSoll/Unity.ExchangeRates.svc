# 📘 Unity Exchange Rates API — Complete Notebook Guide

> Notebook rujukan lengkap untuk memahami setiap feature dan implementation dalam projek ini.
> Disusun mengikut **topik/feature** — bukan mengikut file.
> Setiap topik ada: **Apa**, **Kenapa**, **Macam Mana**, **Kelebihan**, dan **Code Explain**.

---

## 📑 Daftar Kandungan

| # | Topik | Muka |
|---|-------|------|
| 1 | [Layered Architecture](#1--layered-architecture) | Struktur projek 6 layer |
| 2 | [CQRS Pattern (Mediator)](#2--cqrs-pattern-mediator) | Command Query Responsibility Segregation |
| 3 | [Structured Logging (Serilog)](#3--structured-logging-serilog) | Log levels, enricher, config |
| 4 | [Security — Authentication (Future)](#4--security--authentication-future) | JWT/IDP planned |
| 5 | [API Versioning](#5--api-versioning) | URL segment versioning |
| 6 | [Rate Limiting](#6--rate-limiting) | Protect API dari abuse |
| 7 | [Audit Logging (Audit.NET)](#7--audit-logging-auditnet) | Request/response tracking |
| 8 | [CORS Lock-down](#8--cors-lock-down) | Cross-Origin Resource Sharing |
| 9 | [Unit of Work & Repository Pattern](#9--unit-of-work--repository-pattern) | Transaction management & data access |
| 10 | [Validation Pipeline (FluentValidation)](#10--validation-pipeline-fluentvalidation) | Auto-validation, session config |
| 11 | [Background Jobs (Hangfire)](#11--background-jobs-hangfire) | Scheduled daily sync |
| 12 | [HTTP Client & Resilience (Polly)](#12--http-client--resilience-polly) | BNM API call, retry policy |
| 13 | [Global Error Handling](#13--global-error-handling) | Exception middleware |
| 14 | [EF Core Interceptor — Auto Audit Fields](#14--ef-core-interceptor--auto-audit-fields) | CreatedOn/ModifiedOn auto-set |
| 15 | [Business Logic — Sync Flow](#15--business-logic--sync-flow) | Core sync handler, weekend logic |
| 16 | [Dependency Injection Registration](#16--dependency-injection-registration) | Multi-layer DI wiring |
| 17 | [Configuration (appsettings)](#17--configuration-appsettings) | Semua config sections |

---

## 1 — Layered Architecture

### Apa?
Projek dibahagikan kepada 6 projek berasingan, setiap satu ada tanggungjawab tersendiri.

### Kenapa?
- **Separation of Concerns** — setiap layer fokus satu perkara sahaja
- **Testable** — boleh mock satu layer penuh tanpa sentuh yang lain
- **Maintainable** — tambah feature baru tak ganggu layer lain
- **Consistent** — pattern yang sama digunakan dalam Facility service

### Struktur

```
Unity.ExchangeRates.svc/src/
├── Unity.ExchangeRates.Api/              ← Entry point
│   ├── Controllers/                       ← REST endpoints
│   ├── Middlewares/                        ← Exception handler
│   ├── Configurations/                    ← CORS, Audit, Mapper, Logging
│   └── docs/                              ← Dokumentasi
├── Unity.ExchangeRates.Domain/           ← Pure models, zero dependency
│   ├── Models/                            ← Entity classes
│   ├── Events/                            ← Audit event contracts
│   └── Exceptions/                        ← Custom exceptions
├── Unity.ExchangeRates.Repository/       ← Interface sahaja
│   ├── IExchangeRateRepository.cs
│   └── IUnitOfWork.cs
├── Unity.ExchangeRates.Infrastructure/   ← Concrete implementation
│   ├── Data/AppDbContext.cs               ← EF Core DbContext
│   ├── Repositories/                      ← Repository implementation
│   ├── Interceptors/                      ← SaveChanges interceptor
│   └── Migrations/                        ← Database migrations
├── Unity.ExchangeRates.Service/          ← Business logic
│   ├── Mediator/Commands/                 ← Write operations (Sync)
│   ├── Mediator/Queries/                  ← Read operations (GetRate)
│   ├── Behaviors/                         ← Validation pipeline
│   ├── EventHandlers/                     ← Audit log handler
│   └── Configurations/                    ← BnmApiOptions
└── Unity.ExchangeRates.Shared/           ← Cross-cutting
    ├── Jobs/                              ← Hangfire sync job
    └── Services/                          ← Audit dispatcher, HTTP client
```

### Dependency Direction (PENTING!)

```
Api → Service → Domain ← Repository
Api → Infrastructure → Domain
Api → Shared → Service
```

- **Domain** & **Repository** = paling inner, ZERO dependency pada outer layers
- **Infrastructure** implement **Repository** interfaces
- **Api** tie semuanya bersama melalui DI registration

### Kelebihan Pattern Ni
1. Kalau nak tukar database (contoh: dari SQL Server ke PostgreSQL), hanya ubah **Infrastructure** — Service dan Domain tak terkesan
2. Kalau nak tambah endpoint baru, hanya tambah dalam **Api** dan **Service** — Infrastructure tak terkesan
3. Unit test boleh mock **Repository interface** tanpa perlu real database

---

## 2 — CQRS Pattern (Mediator)

### Apa?
CQRS = **Command Query Responsibility Segregation**. Kita pisahkan operasi READ dari operasi WRITE.

- **Query** = baca data (GET endpoint)
- **Command** = tulis/ubah data (POST sync endpoint)

### Kenapa?
- **Single Responsibility** — setiap handler buat SATU kerja sahaja
- **Testable** — test handler secara terasing tanpa HTTP context
- **Scalable** — boleh scale read dan write secara bebas
- **Clean controller** — controller hanya map request → command/query → response

### Macam Mana?

Guna library **Mediator** (source-generated, lebih pantas dari MediatR).

📂 `Api/Program.cs` — Line 34:
```csharp
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
```
- `Scoped` = satu instance Mediator per HTTP request
- Source-generated = compile-time, bukan reflection — lebih pantas

### Flow: GET Request

```
Client → Controller.GetRate(?currency, ?date)
         → _mapper.Map<ExchangeRateQuery>(request)
         → _mediator.Send(query)
           → [RequestValidationBehavior] ← validate input
           → [ExchangeRateQueryHandler]  ← query database (all or single)
         → _mapper.Map<BaseResponse>(result)
       → return JSON
```

📂 `Api/Controllers/ExchangeRateController.cs`:
```csharp
[HttpGet]
public async Task<IActionResult> GetRate([FromQuery] string? currency, [FromQuery] string? date)
{
    if (string.IsNullOrEmpty(date))
        date = DateTime.UtcNow.ToString("yyyy-MM-dd");

    _logger.LogInformation("GetRate request received: currency={currency}, date={date}", currency ?? "ALL", date);
    var request = new ExchangeRateRequest { currency = currency, date = date };
    var query = _mapper.Map<ExchangeRateQuery>(request);         // Map ke Query object
    var result = await _mediator.Send(query);                     // Hantar ke handler
    return ApiResponse<BaseResponse, BaseResult>(                 // Standardize response
        _mapper.Map<BaseResponse>(result.ValueOrDefault), result);
}
```

📂 `Service/Mediator/Queries/ExchangeRates/ExchangeRateQueryHandler.cs`:
```csharp
public async ValueTask<Result<BaseResult>> Handle(ExchangeRateQuery request, CancellationToken cancellationToken)
{
    var createdDate = DateTime.ParseExact(request.date!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // Kalau currency kosong — return SEMUA rates untuk tarikh tu
    if (string.IsNullOrEmpty(request.currency))
    {
        var histories = await _repository.GetAllRatesByDateAsync(createdDate, cancellationToken);
        if (histories.Count == 0)
            return Result.Fail(new NotFoundError() { ... });
        return new BaseResult() { data = histories };
    }

    // Single currency
    var history = await _repository.GetRateByCreatedDateAsync(request.currency!, createdDate, cancellationToken);
    if (history is null)
        return Result.Fail(new NotFoundError() { errorCode = "00404", errorMsg = "No exchange rate data found..." });

    return new BaseResult() { data = history };
}
```

### Flow: POST Request (Sync)

```
Client → Controller.Sync()
         → _mapper.Map<ExchangeRateSyncCommand>(request)
         → _mediator.Send(command)
           → [RequestValidationBehavior]          ← validate date & session
           → [ExchangeRateSyncCommandHandler]     ← call BNM API, save to DB
         → return JSON
```

### Flow: GET Currencies (Latest)

```
Client → Controller.GetCurrencies()
         → new GetCurrenciesQuery()
         → _mediator.Send(query)
           → [GetCurrenciesQueryHandler]  ← query database
         → return JSON
```

📂 `Api/Controllers/ExchangeRateController.cs`:
```csharp
[HttpGet("currencies")]
[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> GetCurrencies()
{
    _logger.LogInformation("GetCurrencies request received");
    var query = new GetCurrenciesQuery();           // Tak perlu mapper — tiada input params
    var result = await _mediator.Send(query);       // Hantar ke handler
    return ApiResponse<BaseResult>(result);          // Return direct — tak perlu map
}
```

📂 `Service/Mediator/Queries/Currencies/GetCurrenciesQuery.cs`:
```csharp
public class GetCurrenciesQuery : IRequest<Result<BaseResult>>
{
    // Empty — tak perlu params, fetch semua currencies
}
```

📂 `Service/Mediator/Queries/Currencies/GetCurrenciesQueryHandler.cs`:
```csharp
public async ValueTask<Result<BaseResult>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
{
    var currencies = await _repository.GetActiveCurrenciesAsync(cancellationToken);

    var result = currencies.Select(c => new
    {
        currencyCode = c.CurrencyCode,    // CurrencyCode dari database
        currencyName = c.CurrencyName
    }).ToList();

    return new BaseResult() { data = result };
}
```

**Kenapa endpoint ni?**
- Client perlu tahu currencies apa yang available sebelum call sync/getrate
- Tak perlu hardcode currency list dalam frontend
- Kalau tambah currency baru dalam DB, frontend auto nampak

**URLs:**
- `GET /api/v1/exchange-rates` — semua rates (optional `?currency=` & `?date=`)
- `GET /api/v1/exchange-rates/currencies` — senarai currencies

**Response contoh (all rates):**
```json
{
  "status": "Success",
  "data": [
    { "currencyCode": "usd", "rateDate": "2026-03-03", "buyingRate": 4.35, ... },
    { "currencyCode": "gbp", "rateDate": "2026-03-03", "buyingRate": 5.45, ... }
  ]
}
```

**Response contoh (currencies):**
```json
{
  "status": "Success",
  "data": [
    { "currencyCode": "usd", "currencyName": "US Dollar" },
    { "currencyCode": "gbp", "currencyName": "British Pound" }
  ]
}
```

### Kenapa `ISender` bukan `IMediator`?

📂 `Api/Controllers/ExchangeRateController.cs` — Line 21:
```csharp
private readonly ISender _mediator;  // Bukan IMediator
```
- `ISender` = hanya `Send()` (command/query)
- `IMediator` = `Send()` + `Publish()` (notifications)
- Controller hanya perlu send — **Interface Segregation Principle** (SOLID)

### Kelebihan
1. Controller jadi sangat thin — hanya 5-6 baris per method
2. Business logic 100% dalam handlers — senang test
3. Validation automatik melalui pipeline behavior
4. Senang trace flow — setiap operation ada handler yang jelas

---

## 3 — Structured Logging (Serilog)

### Apa?
Logging guna **Serilog** — library structured logging yang simpan data sebagai key-value pairs, bukan plain text.

### Kenapa Serilog, bukan default `ILogger`?
- **Structured data** — `currency=usd, date=2026-02-27` vs `"GetRate for usd on 2026-02-27"`. Boleh search/filter by field
- **Multiple sinks** — tulis ke File DAN Console serentak
- **Rolling files** — log file rotate mengikut interval (setiap jam)
- **Configuration-driven** — tukar log level tanpa recompile, cuma ubah appsettings
- **Custom enrichers** — tambah method name, timestamp, etc. automatik

### Config

📂 `Api/Program.cs` — Lines 233-240:
```csharp
static void ConfigureLog(IHostBuilder hostBuilder)
{
    hostBuilder.UseSerilog((context, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration);  // Baca dari appsettings
        config.Enrich.WithMethodName();                        // Custom enricher
    });
}
```

📂 `appsettings.json` — Serilog section:
```json
"Serilog": {
    "MinimumLevel": {
        "Default": "Information",
        "Override": {
            "Microsoft": "Warning",
            "System": "Warning"
        }
    },
    "WriteTo": [
        { "Name": "Console" },
        {
            "Name": "File",
            "Args": {
                "path": "./logs/log-.txt",
                "rollingInterval": "Hour",
                "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}{MethodName} {Message:lj}{NewLine}{Exception}"
            }
        }
    ]
}
```

**Explain config:**
- `MinimumLevel: Information` — log dari Information ke atas (Debug diabaikan dalam production)
- `Override Microsoft: Warning` — framework logs hanya Warning ke atas (kurangkan noise)
- `rollingInterval: Hour` — file baru setiap jam. Contoh: `log-2026030210.txt`
- `outputTemplate` — format: `[2026-03-02 10:05:23 INF] ExchangeRateController.GetRate request received...`

### Custom Enricher — LogMethodNameEnricher

📂 `Api/Configurations/Logging/LogMethodNameEnricher.cs`

**Apa?** Tambah nama method secara automatik dalam setiap log entry.

**Kenapa?** Default Serilog hanya log class name (`SourceContext`). Dengan enricher ni, log tunjuk class DAN method — senang trace.

### Log Level Strategy

| Level | Bila Guna | Contoh |
|-------|----------|--------|
| **Debug** | Detail internal, dev sahaja | `"Repository: GetActiveCurrenciesAsync called"` |
| **Information** | Business milestones | `"Sync completed. Synced 8/8 currencies"` |
| **Warning** | Recoverable issues | `"No rate found in DB for currency=xyz"` |
| **Error** | Operation failures | `"BNM API returned 404 for JPY"` |
| **Critical** | System crashes | `"Hangfire job crashed unexpectedly"` |

### Contoh Penggunaan dalam Projek

📂 `Service/Mediator/Commands/.../ExchangeRateSyncCommandHandler.cs`:
```csharp
_logger.LogInformation("Completed. Synced {synced}/{total} currencies for {date}",
    syncedCount, currencies.Count, targetDateStr);
```
- **Information** — business milestone. Sync berjaya.

📂 `Infrastructure/UnitOfWork.cs`:
```csharp
_logger.LogWarning("UnitOfWork: Transaction rolled back");
```
- **Warning** — rollback bermakna something went wrong.

📂 `Shared/Jobs/ExchangeRateSyncJob.cs`:
```csharp
_logger.LogCritical(ex, "Hangfire SyncDaily: Job crashed unexpectedly");
```
- **Critical** — highest severity. Job crash = data takde untuk hari tu.

### Kelebihan
1. **Searchable** — cari semua log untuk `currency=usd` tanpa regex
2. **Filterable** — show hanya Error level dalam production
3. **Configurable** — tukar level on-the-fly tanpa redeploy
4. **Traceable** — method name + class name dalam setiap entry
5. **Rolling files** — tak jadi satu file gergasi

---

## 4 — Security — Authentication (Future)

### Status Semasa
Buat masa ini, API **belum ada authentication layer** — semua endpoint terbuka. Ini keputusan sementara sehingga IDP (Identity Provider) siap.

### Rancangan Masa Depan
- **JWT Bearer Authentication** — akan diimplementasi apabila IDP didaftarkan
- Token validation (issuer, audience, signing key) akan dibaca dari config
- Swagger UI akan ada "Authorize" button untuk input Bearer token
- `[Authorize]` attribute akan ditambah pada controller/endpoint yang perlu dilindungi

### Security Layers Yang Sudah Ada
Walaupun belum ada authentication, API masih dilindungi oleh:
1. **Rate Limiting** — hadkan request per IP (Section 6)
2. **CORS Lock-down** — hanya domain yang dibenarkan boleh call (Section 8)
3. **Audit Logging** — setiap request direkod (Section 7)
4. **Exception Handler** — error handling yang proper (Section 13)

---

## 5 — API Versioning

### Apa?
Setiap endpoint ada version number dalam URL: `/api/v1/exchangerates/...`

### Kenapa?
- **Breaking changes** — nanti V2 boleh ada different response format tanpa rosak V1 clients
- **Backward compatibility** — V1 clients tetap boleh call walaupun V2 dah live
- **Industry standard** — kebanyakan public API guna versioning
- **Same as Facility** — consistent across projek

### Macam Mana?

📂 `Api/Program.cs` — Lines 113-127:
```csharp
static void ConfigureApiVersioning(IServiceCollection services)
{
    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;   // Client lama masih boleh call
        options.DefaultApiVersion = new ApiVersion(1, 0);     // Default = v1.0
        options.ReportApiVersions = true;                     // Response header tunjuk supported versions
        options.ApiVersionReader = new UrlSegmentApiVersionReader();  // Version dari URL path
    })
    .AddApiExplorer(setup =>
    {
        setup.GroupNameFormat = "'v'VVV";           // Format: v1, v2
        setup.SubstituteApiVersionInUrl = true;     // Auto replace {version} dalam route
    });
}
```

**Line-by-line explain:**
- `AssumeDefaultVersionWhenUnspecified` — kalau call `/api/exchangerates` tanpa version, assume v1
- `UrlSegmentApiVersionReader` — version dibaca dari URL path (bukan query string atau header)
- `SubstituteApiVersionInUrl` — route `api/v{version:apiVersion}/...` auto jadi `api/v1/...`
- `ReportApiVersions` — response header include `api-supported-versions: 1.0`

📂 `Api/Controllers/ExchangeRateController.cs`:
```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/exchangerates")]
```
- Controller di-tag sebagai version 1.0
- Route template auto-resolve: `v{version:apiVersion}` → `v1`

### Swagger Integration

📂 `Api/Program.cs` — `ConfigureSwagger()`:
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
- Loop setiap version → buat Swagger doc per version
- Swagger UI ada dropdown untuk pilih version

### Kelebihan
1. **URL Segment** — paling clean dan readable
2. **Auto Swagger** — new version auto appear dalam Swagger UI
3. **Non-breaking** — V1 clients tak terkesan bila V2 direlease
4. **Same pattern as Facility** — consistent approach

### Macam Mana Nak Implement V2? (Panduan Masa Depan)

Katakan nanti requirement berubah — V2 nak return response format baru (contoh: tambah field `lastUpdated`, ubah structure). Tapi V1 clients masih active. Caranya:

#### Step 1 — Buat Controller Baru untuk V2

📂 `Api/Controllers/ExchangeRateV2Controller.cs` **(NEW)**:
```csharp
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/exchange-rates")]
public class ExchangeRateV2Controller : BaseApiController
{
    // Constructor sama macam V1

    [HttpGet("{currency}")]
    public async Task<IActionResult> GetRate(string currency, [FromQuery] string? date)
    {
        // V2: response format baru / logic baru
        var query = new ExchangeRateV2Query { ... };
        var result = await _mediator.Send(query);
        return Ok(result);  // Format berbeza dari V1
    }
}
```

**V1 controller KEKAL MACAM SEDIA ADA** — langsung tak sentuh.

#### Step 2 — Dua-dua Version Jalan Serentak

```
/api/v1/exchange-rates/usd    ← V1 clients (format lama)
/api/v2/exchange-rates/usd    ← V2 clients (format baru)
```

ASP.NET auto-route ke controller yang betul berdasarkan `[ApiVersion]` attribute.

#### Step 3 — Swagger Auto Detect

Sebab `ConfigureSwagger()` loop semua versions:
```csharp
foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
    swagger.SwaggerDoc(description.GroupName, ...);
```
Swagger UI auto ada **dropdown**: `v1` dan `v2`. Developer boleh test kedua-dua.

#### Step 4 — Deprecate V1 (Bila Dah Ready)

Tukar attribute dalam V1 controller:
```csharp
[ApiVersion("1.0", Deprecated = true)]  // ← Tambah Deprecated = true
```

**Apa jadi?**
- V1 **MASIH BOLEH dipanggil** — tak hilang, tak rosak
- Response header: `api-deprecated-versions: 1.0`
- Swagger UI tunjuk label **(deprecated)** pada V1 endpoints
- Bagi masa client untuk migrate ke V2

#### Step 5 — Remove V1 (Optional, Masa Depan)

Bila semua clients dah migrate ke V2, baru delete `ExchangeRateController.cs` (V1).

#### Struktur Folder Nanti

```
Controllers/
├── ExchangeRateController.cs          ← V1 (kekal / deprecated)
├── ExchangeRateV2Controller.cs        ← V2 (baru)
└── Base/BaseApiController.cs          ← Shared base

Service/Mediator/Queries/
├── ExchangeRates/                     ← V1 handlers
│   ├── ExchangeRateQuery.cs
│   └── ExchangeRateQueryHandler.cs
├── ExchangeRatesV2/                   ← V2 handlers (kalau logic berbeza)
│   ├── ExchangeRateV2Query.cs
│   └── ExchangeRateV2QueryHandler.cs
└── Currencies/                        ← Shared (kedua-dua version guna)
    ├── GetCurrenciesQuery.cs
    └── GetCurrenciesQueryHandler.cs
```

#### Summary Versioning

| Situasi | Action |
|---------|--------|
| Nak tambah V2 | Buat controller baru + `[ApiVersion("2.0")]` |
| V1 masih active | **JANGAN SENTUH** V1 controller |
| V1 nak deprecate | Tambah `Deprecated = true` pada V1 |
| V1 nak remove | Delete controller HANYA bila semua client dah migrate |
| Endpoint dikongsi (currencies) | Letak dalam V1 controller — V2 boleh inherit atau guna sama |

**Sekarang infrastructure versioning dah 100% ready.** Bila nak buat V2, hanya perlu tambah controller + handler baru. Takde config tambahan.

---

## 6 — Rate Limiting

### Apa?
Hadkan bilangan request yang satu IP boleh buat dalam tempoh masa tertentu. Exceed = **429 Too Many Requests**.

### Kenapa?
- **Prevent abuse** — block bots/attackers yang spam API
- **Protect resources** — database connection pool ada limit
- **Fair usage** — semua client dapat share yang adil
- **Production safety** — API akan digunakan Life Asia, kena protect

### Macam Mana?

Guna package **AspNetCoreRateLimit**.

📂 `Api/Program.cs` — Lines 221-228:
```csharp
static void ConfigureRateLimit(IServiceCollection services, IConfiguration configuration)
{
    services.AddMemoryCache();                    // Counter simpan dalam RAM
    services.Configure<IpRateLimitOptions>(       // Baca config dari appsettings
        configuration.GetSection(nameof(IpRateLimitOptions)));
    services.Configure<IpRateLimitPolicies>(
        configuration.GetSection(nameof(IpRateLimitPolicies)));
    services.AddInMemoryRateLimiting();           // In-memory counter
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
}
```

📂 `Api/Program.cs` — Line 59 (Middleware):
```csharp
app.UseIpRateLimiting();  // Selepas API Key auth
```

**Kenapa selepas API Key auth?**
- Request tanpa valid key dah kena reject oleh `ApiKeyAuthMiddleware`
- Request yang sampai rate limiter = authenticated requests sahaja
- Tak waste counter untuk invalid requests

### Config

📂 `appsettings.Development.json`:
```json
"IpRateLimitOptions": {
    "EnableEndpointRateLimiting": true,
    "HttpStatusCode": 429,
    "RealIpHeader": "X-Real-Ip",
    "QuotaExceededResponse": {
        "Content": "{ \"status\": \"Failed\", \"errorCode\": \"00429\", \"errorMsg\": \"Quota exceeded. Maximum allowed: {0} per {1}.\" }",
        "ContentType": "application/json",
        "StatusCode": 429
    },
    "GeneralRules": [
        { "Endpoint": "*", "Period": "1s", "Limit": 500 }
    ]
}
```

**Explain:**
- `500 req/s per IP` — cukup untuk normal usage
- `429` — standard HTTP code untuk "too many requests"
- Custom JSON response — bukan blank error, client tahu apa masalah
- `*` endpoint — apply ke SEMUA endpoints

### Kelebihan
1. **Config-driven** — tukar limit tanpa recompile
2. **Per-IP** — setiap client ada limit sendiri
3. **Custom response** — JSON format yang bermakna
4. **In-memory** — zero latency overhead

---

## 7 — Audit Logging (Audit.NET)

### Apa?
Rekod setiap API request dan response sebagai audit trail — siapa call, bila, apa request, apa response.

### Kenapa?
- **Security** — trace unauthorized access attempts
- **Compliance** — Life Asia (insurance) perlukan audit trail untuk regulatory
- **Debugging** — check balik "request apa masuk, response apa keluar"
- **Accountability** — setiap action ada rekod

### Macam Mana?

Event-driven pattern melalui Mediator — 6 class merentasi 4 layer:

```
Request masuk
  → UseAuditLog() middleware capture request/response
    → AuditLogEventDispatcher publish event via Mediator
      → AuditLogEventHandler create AuditScope
        → FileDataProvider tulis JSON ke audit-logs/
```

### Class-by-Class

#### 1. Middleware Config

📂 `Api/Configurations/AuditConfigurationBuilderExtensions.cs`:
```csharp
public static IApplicationBuilder UseAuditLog(this WebApplication builder)
{
    builder.UseAuditMiddleware(_ => _
        .FilterByRequest(rq => !rq.Path.Value.EndsWith("favicon.ico"))  // Skip favicon
        .WithEventType("{verb}:{url}")             // Contoh: "GET:/api/v1/exchangerates/usd/2026-02-28"
        .IncludeHeaders()                          // Capture request headers
        .IncludeResponseHeaders()                  // Capture response headers
        .IncludeRequestBody()                      // Capture JSON body
        .IncludeResponseBody(ctx =>
            ctx.Response.StatusCode != 200));       // Response body HANYA kalau bukan 200

    builder.Use(async (context, next) => {
        context.Request.EnableBuffering();          // Allow multiple reads of request body
        await next();
    });

    return builder;
}
```

**Kenapa `EnableBuffering()`?** Request body stream hanya boleh dibaca SEKALI. Audit baca sekali, controller baca sekali — dua kali. Buffering allow multiple reads.

**Kenapa response body hanya kalau bukan 200?** Jimat storage. Kalau 200 OK, data tu dah dalam database. Kalau error, PERLU simpan response untuk diagnosis.

#### 2. Event Contracts (Domain Layer)

📂 `Domain/Events/IEvent.cs`:
```csharp
public interface IEvent : INotification { }  // Base untuk semua domain events
```

📂 `Domain/Events/IAuditLogEvent.cs`:
```csharp
public interface IAuditLogEvent : IEvent
{
    string EventType { get; }      // "ExchangeRateSync"
    string ReferenceId { get; }    // ID untuk trace
    string Message { get; }        // "Rate synced successfully"
    object Data { get; }           // Actual data object
}
```

📂 `Domain/Events/AuditLogEvent.cs`:
```csharp
public class AuditLogEvent : IAuditLogEvent
{
    public string EventType => Data?.GetType().Name ?? "Unknown";  // Auto dari type name
    // ... properties
}
```

**Kenapa dalam Domain layer?** Domain = pure contracts, zero dependency. Mana-mana layer boleh reference.

#### 3. Handler (Service Layer)

📂 `Service/EventHandlers/AuditLogEventHandler.cs`:
```csharp
public async ValueTask Handle(AuditLogEvent notification, CancellationToken cancellationToken)
{
    using (var audit = await AuditScope.CreateAsync(notification.EventType, () => notification.Data))
    {
        audit.SetCustomField("ReferenceId", notification.ReferenceId);
        audit.SetCustomField("IpAddress",
            _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        audit.Comment(notification.Message);
    }
}
```

- `AuditScope` tulis JSON file automatik
- Custom fields: `ReferenceId` dan `IpAddress` untuk tracing

#### 4. Dispatcher (Shared Layer)

📂 `Shared/Services/AuditLogEventDispatcher.cs`:
```csharp
public class AuditLogEventDispatcher : IAuditLogEventDispatcher
{
    private readonly IMediator _mediator;

    public async Task DispatchAsync(IAuditLogEvent auditEvent)
    {
        await _mediator.Publish(auditEvent as INotification);  // Publish via Mediator
    }
}
```

#### 5. Data Provider Config

📂 `Service/ServiceCollectionExtensions.cs`:
```csharp
Audit.Core.Configuration.DataProvider = new FileDataProvider(cfg => cfg.Directory("audit-logs"));
services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
```

- `FileDataProvider` → JSON files dalam `audit-logs/` folder
- Production: boleh tukar ke database-based provider

### Kelebihan
1. **Automatic** — setiap request auto-captured tanpa code tambahan dalam controller
2. **Event-driven** — decoupled dari business logic
3. **Extensible** — tukar storage tanpa ubah handler
4. **Traceable** — IP address, timestamp, request body — semua ada

---

## 8 — CORS Lock-down

### Apa?
CORS = Cross-Origin Resource Sharing. Kawal domain mana yang boleh call API kita dari browser.

### Kenapa?
- **Security** — prevent unauthorized websites dari call API
- **Production safety** — hanya Life Asia domain yang dibenarkan
- **Phase 2 hardening** — restrict dari "allow all" ke specific origins

### Macam Mana?

📂 `Api/Program.cs` — Lines 186-216:
```csharp
static void ConfigureCors(IWebHostEnvironment environment, IServiceCollection services, IConfiguration configuration)
{
    var corsOptions = configuration.GetSection(nameof(CorsOptions)).Get<CorsOptions>();

    if (corsOptions == null)
    {
        if (environment.IsDevelopment())
        {
            // Dev mode: allow semua — senang development
            builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            // Production TANPA config = BLOCK! App tak start
            throw new InvalidOperationException("Cors is not configured correctly in appsettings.");
        }
    }
    else
    {
        // Production: hanya origins yang listed
        builder.WithOrigins(corsOptions.Origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}
```

**Kenapa `throw` kalau production takde config?**
- Safety net — lebih baik app tak start dari terdedah tanpa CORS protection
- Force DevOps team sediakan config sebelum deploy

📂 `appsettings.Development.json`:
```json
"CorsOptions": {
    "Origins": "#{CORS_ORIGINS}#"
}
```
- `#{CORS_ORIGINS}#` — placeholder yang CI/CD pipeline replace dengan domain sebenar

---

## 9 — Unit of Work & Repository Pattern

### Apa?

Dua pattern yang bekerja bersama:
- **Repository** — abstraction untuk data access (CRUD operations)
- **Unit of Work** — wrap repository dengan transaction management

### Kenapa Repository?
- **Abstraction** — business logic tak tahu EF Core wujud
- **Testable** — mock `IExchangeRateRepository` dalam unit tests
- **Swappable** — boleh tukar dari EF Core ke Dapper tanpa sentuh handlers

### Kenapa Unit of Work?
- **Transaction** — semua operations save SERENTAK atau rollback SERENTAK
- **Data integrity** — takde partial sync (separuh currency je masuk database)
- **Centralized** — satu tempat untuk `SaveChanges`, `Commit`, `Rollback`

### Repository Interface

📂 `Repository/IExchangeRateRepository.cs`:
```csharp
public interface IExchangeRateRepository
{
    Task<List<Currency>> GetActiveCurrenciesAsync(CancellationToken ct);
    Task<ExchangeRateHistory?> GetRateByCreatedDateAsync(string currencyCode, DateTime createdDate, CancellationToken ct);
    Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken ct);
}
```

- 3 operations sahaja — keep interface focused
- Return `Task` — semua async

### Repository Implementation

📂 `Infrastructure/Repositories/ExchangeRateRepository.cs`:
```csharp
public async Task<ExchangeRateHistory?> GetRateByCreatedDateAsync(
    string currencyCode, DateTime createdDate, CancellationToken cancellationToken)
{
    _logger.LogDebug("Repository: GetRateByCreatedDateAsync for {CurrencyCode}, {CreatedDate}", ...);

    var history = await _context.ExchangeRateHistories
        .FirstOrDefaultAsync(h =>
            h.CurrencyCode == currencyCode &&
            h.CreatedOn.Date == createdDate.Date,    // Compare DATE sahaja, ignore time
            cancellationToken);

    return history;
}
```

**Kenapa `CreatedOn.Date` bukan `RateDate`?**
- `CreatedOn` = bila data disimpan (hari ni). `RateDate` = tarikh BNM
- Weekend: 3 rows boleh ada same `RateDate` (Friday) tapi different `CreatedOn` (Sat/Sun/Mon)
- User query "kadar hari ini" — bermaksud data yang di-sync hari ini = `CreatedOn`

### Unit of Work Interface

📂 `Repository/IUnitOfWork.cs`:
```csharp
public interface IUnitOfWork : IDisposable
{
    IExchangeRateRepository ExchangeRates { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task BeginTransactionAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}
```

### Unit of Work Implementation

📂 `Infrastructure/UnitOfWork.cs`:
```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;    // Nullable — boleh ada atau takde

    public IExchangeRateRepository ExchangeRates { get; }

    public async Task BeginTransactionAsync(CancellationToken ct)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
        _logger.LogDebug("UnitOfWork: Transaction started");
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        var count = await _context.SaveChangesAsync(ct);
        _logger.LogInformation("UnitOfWork: Persisted {Count} changes", count);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_transaction is null) return;    // Guard — elak NullReferenceException
        await _transaction.CommitAsync(ct);
        _logger.LogDebug("UnitOfWork: Transaction committed");
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        _logger.LogWarning("UnitOfWork: Transaction rolled back");  // Warning — something wrong
    }
}
```

**Kenapa `RollbackAsync` log Warning?** Rollback bermakna exception berlaku — ops perlu tahu.

### Flow Penggunaan dalam Sync Handler

```csharp
await _unitOfWork.BeginTransactionAsync(ct);          // 1. Mula transaction

foreach (var curr in currencies)
{
    await _unitOfWork.ExchangeRates.AddRateHistoryAsync(history, ct);  // 2. Add data (belum save)
}

await _unitOfWork.SaveChangesAsync(ct);               // 3. Save semua serentak
await _unitOfWork.CommitAsync(ct);                    // 4. Commit transaction

// ATAU kalau error:
await _unitOfWork.RollbackAsync(ct);                  // Undo semua
```

### Kelebihan
1. **All-or-nothing** — semua currencies sync atau semua rollback
2. **Single SaveChanges** — 8 currencies = 1 database call, bukan 8
3. **Testable** — mock `IUnitOfWork` dalam tests
4. **Logged** — setiap transaction action ada log entry

---

## 10 — Validation Pipeline (FluentValidation)

### Apa?
Auto-validate setiap Command/Query SEBELUM handler jalan. Kalau input tak valid, handler LANGSUNG tak dipanggil.

### Kenapa?
- **DRY** — validation tulis sekali, apply automatik
- **Fail fast** — invalid input ditolak awal, jimat processing
- **Readable** — rules dibaca macam English
- **Testable** — unit test setiap rule secara terasing
- **Separation** — validation logic terpisah dari business logic

### Pipeline Behavior

📂 `Service/Behaviors/RequestValidationBehavior.cs`:
```csharp
public class RequestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;  // SEMUA validators untuk type ni

    public async ValueTask<TResponse> Handle(TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(message);

        // Run SEMUA validators SERENTAK (parallel)
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = validationResults.SelectMany(r => r.Errors)
            .Where(f => f != null).ToList();

        if (failures.Any())
        {
            _logger.LogWarning("Validation Error: {@errors}", errors);
            return new TResponse().WithErrors(errors);  // STOP — handler tak dipanggil
        }

        return await next(message, ct);  // Valid — teruskan ke handler
    }
}
```

**Kenapa `IPipelineBehavior`?** Macam middleware untuk Mediator. Intercept setiap request sebelum handler.

**Kenapa `Task.WhenAll`?** Run semua validators serentak — performance. Kalau ada 3 validators, jalan parallel.

### Session Config Validator

📂 `Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandValidator.cs`:
```csharp
public sealed class ExchangeRateSyncCommandValidator : AbstractValidator<ExchangeRateSyncCommand>
{
    private static readonly HashSet<string> ValidSessions = new() { "0900", "1130", "1200", "1700" };

    public ExchangeRateSyncCommandValidator()
    {
        RuleFor(c => c.date)
            .NotEmpty().WithErrorCode("00400").WithMessage("Date is required.")
            .Matches(@"^\d{4}-\d{2}-\d{2}$").WithErrorCode("00400")
            .WithMessage("Date must be in yyyy-MM-dd format.");

        RuleFor(c => c.session)
            .Must(s => ValidSessions.Contains(s!))
            .When(c => !string.IsNullOrEmpty(c.session))    // Validate HANYA kalau provided
            .WithErrorCode("00400")
            .WithMessage("Session must be one of: 0900, 1130, 1200, 1700.");
    }
}
```

### Session Values — Apa Maksud?

| Session | Masa | Makna |
|---------|------|-------|
| `0900` | 9:00 AM | Opening rate (pagi) |
| `1130` | 11:30 AM | Mid-morning rate |
| `1200` | 12:00 PM | Noon rate |
| `1700` | 5:00 PM | **Closing rate** (petang) |

**Kenapa default `1700`?** Closing rate paling stabil — reflect pergerakan sepanjang hari. Config dalam `BnmApiSettings.DefaultSession`.

**Kenapa session optional?** Kalau tak provide, handler guna default dari config. Ini flexible — Hangfire job tak specify session, auto guna 1700.

**Kenapa `HashSet` bukan `List`?** `HashSet.Contains()` = O(1) constant time vs `List.Contains()` = O(n) linear. Lebih efficient.

### DI Registration

📂 `Service/ServiceCollectionExtensions.cs`:
```csharp
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;    // First error stops
ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());        // Auto-discover
```

- `CascadeMode.Stop` — kalau rule pertama fail, stop. Tak run rules seterusnya
- `AddValidatorsFromAssembly` — scan assembly, auto-register SEMUA validators

---

## 11 — Background Jobs (Hangfire)

### Apa?
Hangfire = library untuk scheduled background jobs. Dalam projek ni, dia trigger daily sync setiap tengah malam.

### Kenapa Hangfire?
- **Persistent** — job state simpan dalam SQL Server, survive app restart
- **Dashboard** — web UI untuk monitor jobs
- **Retry** — auto-retry kalau job fail
- **Cron** — flexible scheduling

### Config

📂 `Shared/ServiceCollectionExtensions.cs`:
```csharp
services.AddHangfire(config =>
    config.UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection")));
services.AddHangfireServer();
services.AddScoped<IExchangeRateSyncJob, ExchangeRateSyncJob>();
```

📂 `Api/Program.cs` — Lines 87-91:
```csharp
RecurringJob.AddOrUpdate<IExchangeRateSyncJob>(
    "daily-exchange-rate-sync",                           // Job ID
    job => job.SyncDailyAsync(CancellationToken.None),    // Method to call
    "0 0 * * *",                                          // Cron: setiap hari 12:00AM
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });  // Ikut timezone server
```

Cron `0 0 * * *` = minit 0, jam 0, setiap hari, setiap bulan, setiap hari minggu = **12AM daily**.

### Job Implementation

📂 `Shared/Jobs/ExchangeRateSyncJob.cs`:
```csharp
public async Task SyncDailyAsync(CancellationToken cancellationToken = default)
{
    try
    {
        var yesterday = DateTime.Now.Date.AddDays(-1).ToString("yyyy-MM-dd");

        _logger.LogInformation("Hangfire SyncDaily: Starting. TargetDate={Target}", yesterday);

        var command = new ExchangeRateSyncCommand { date = yesterday };
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailed)
            _logger.LogError("Sync failed for {Target}. Errors={Errors}", ...);
        else
            _logger.LogInformation("Sync succeeded for {Target}.", yesterday);
    }
    catch (Exception ex)
    {
        _logger.LogCritical(ex, "Hangfire SyncDaily: Job crashed unexpectedly");
    }
}
```

**Kenapa yesterday?** Job run 12AM hari ni → BNM dah publish rate 5PM semalam → ambil semalam punya.

**Kenapa Mediator.Send?** Reuse EXACT SAME handler. POST endpoint dan Hangfire guna handler yang sama — zero duplication.

**Kenapa `LogCritical`?** Job crash = data TAKDE untuk hari tu = critical production issue.

### Weekend Logic

| Job Run | Yesterday | ResolveBusinessDate | Rate |
|---------|-----------|-------------------|------|
| Selasa 12AM | Isnin | Isnin ✅ | Isnin 1700 |
| Sabtu 12AM | Jumaat | Jumaat ✅ | Jumaat 1700 |
| Ahad 12AM | Sabtu → | **Jumaat** | Jumaat 1700 |
| Isnin 12AM | Ahad → | **Jumaat** | Jumaat 1700 |

---

## 12 — HTTP Client & Resilience (Polly)

### Apa?
BNM API client guna **HttpClientFactory** dengan **Polly retry policy** — kalau BNM down sementara, auto retry.

### Kenapa HttpClientFactory?
- **Connection pool** — reuse connections, avoid socket exhaustion
- **Named clients** — pre-configured dengan BaseURL, headers, timeout
- **Lifetime management** — factory handle HttpClient lifecycle

### Kenapa Polly?
- **Resilience** — BNM boleh down sementara (maintenance, network)
- **Transient errors** — HTTP 500, 503, timeout — biasanya recover sendiri
- **Auto retry** — developer tak perlu tulis retry logic manual

### Implementation

📂 `Shared/ServiceCollectionExtensions.cs`:
```csharp
services.AddHttpClient("BnmClient", (serviceProvider, client) =>
{
    var settings = serviceProvider
        .GetRequiredService<IOptions<BnmApiOptions>>().Value;

    client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + '/');
    client.DefaultRequestHeaders.Add("Accept", settings.AcceptHeader);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddPolicyHandler(BuildRetryPolicy());

private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()       // Handle 5xx and 408 errors
        .WaitAndRetryAsync(new[]
        {
            TimeSpan.FromSeconds(1),      // 1st retry: tunggu 1 saat
            TimeSpan.FromSeconds(2),      // 2nd retry: tunggu 2 saat
            TimeSpan.FromSeconds(5)       // 3rd retry: tunggu 5 saat
        });
}
```

**Retry pattern:** Wait 1s → retry → wait 2s → retry → wait 5s → retry → give up.

**Kenapa increasing delays?** Give server time to recover. Kalau server overloaded, retry immediately akan tambah beban.

---

## 13 — Global Error Handling

### Apa?
Middleware yang catch SEMUA unhandled exceptions dan return proper JSON response.

### Kenapa?
- **DRY** — tulis sekali, cover semua endpoint
- **Consistent** — semua error return same JSON format
- **Safe** — user tak nampak stack trace (security risk)
- **Categorized** — different log levels untuk different error types

### Implementation

📂 `Api/Middlewares/ExceptionHandlerMiddleware.cs`:
```csharp
public async Task Invoke(HttpContext context)
{
    try
    {
        await _next(context);  // Cuba jalankan request
    }
    catch (Exception error)
    {
        switch (error)
        {
            case ExchangeRatesDomainException:
                _logger.LogWarning(...);                       // Expected — business rule violation
                response.StatusCode = 400;                     // Bad Request
                break;

            case ValidationException e:
                _logger.LogWarning(...);                       // Expected — input tak valid
                response.StatusCode = 400;
                resultObject.message = e.Errors.First()...;   // First error message
                break;

            default:
                _logger.LogError(...);                         // Unexpected — real error!
                response.StatusCode = 500;                     // Internal Server Error
                break;
        }

        await response.WriteAsync(resultObject.ToString());   // JSON response
    }
}
```

**Kenapa Warning untuk domain/validation errors?**
- Bukan error sebenar — user hantar input salah, itu NORMAL
- `LogError` reserved untuk benda yang betul-betul salah
- Ops team monitor Error level — tak nak banjir dengan false alarms

---

## 14 — EF Core Interceptor — Auto Audit Fields

### Apa?
Interceptor yang auto-set `CreatedOn` dan `ModifiedOn` setiap kali `SaveChanges()` dipanggil.

### Kenapa?
- **DRY** — tak perlu manual `entity.CreatedOn = DateTime.Now` dalam setiap handler
- **Foolproof** — developer tak boleh lupa set timestamp
- **Audit trail** — setiap record ada timestamp bila created/modified

### Implementation

📂 `Infrastructure/Interceptors/EntitySaveChangeInterceptor.cs`:
```csharp
private void UpdateEntities(DbContext? context)
{
    foreach (var entry in context.ChangeTracker.Entries<BaseEntity<int>>())
    {
        if (entry.State == EntityState.Added)
            entry.Entity.CreatedOn = DateTime.Now;          // New record → set CreatedOn

        if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            entry.Entity.ModifiedOn = DateTime.Now;         // New or updated → set ModifiedOn
    }
}
```

- `ChangeTracker.Entries<BaseEntity<int>>()` — scan SEMUA tracked entities
- `EntityState.Added` → baru masuk → set BOTH CreatedOn dan ModifiedOn
- `EntityState.Modified` → update → set ModifiedOn sahaja
- Run untuk KEDUA-DUA `BaseEntity<int>` dan `BaseEntity<string>` (Currency guna string key)

---

## 15 — Business Logic — Sync Flow

### Apa?
Core handler yang fetch kadar dari BNM API dan simpan ke database.

📂 `Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandHandler.cs`

### Complete Flow

```
1. Parse date input (yyyy-MM-dd)
2. ResolveBusinessDate() — weekend → Friday
3. Determine session (provided or default 1700)
4. Load active currencies dari database
5. Begin database transaction
6. Loop setiap currency:
   a. Build BNM API URL
   b. Call BNM API (with Polly retry)
   c. Parse JSON response
   d. Create ExchangeRateHistory entity
   e. Add to database context
7. SaveChanges (batch — semua serentak)
8. Commit transaction
9. Return result "Synced X of Y currencies"
```

### ResolveBusinessDate

```csharp
private static DateTime ResolveBusinessDate(DateTime date)
{
    while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        date = date.AddDays(-1);    // Tolak satu hari
    return date;
}
```

- `static` — pure function, tak perlu instance state
- `while` loop — handle Saturday (→ Friday) DAN Sunday (→ Saturday → Friday)
- **Kenapa?** BNM tak publish rate pada weekend

### Error Handling dalam Loop

```csharp
if (!response.IsSuccessStatusCode)
{
    _logger.LogError("Failed to fetch {currency}. BNM returned {StatusCode}", ...);
    continue;  // Skip, teruskan currency seterusnya
}
```

**Kenapa `continue` bukan `throw`?**
- Kalau JPY fail tapi USD ok, kita masih nak sync USD
- **Partial sync lebih baik dari total failure**
- Log record mana yang fail — boleh investigate kemudian

### Transaction Rollback

```csharp
catch (Exception ex)
{
    await _unitOfWork.RollbackAsync(cancellationToken);    // Undo SEMUA
    _logger.LogError(ex, "Transaction rolled back.");
    return Result.Fail(new GeneralError() { errorCode = "00500", errorMsg = ex.Message });
}
```

- Database crash, network error → rollback ALL data yang dah add
- **Data integrity** — takde partial/corrupt data dalam database

---

## 16 — Dependency Injection Registration

### Apa?
Setiap layer register services dia sendiri. `Program.cs` panggil registration method dari setiap layer.

### Kenapa Multi-Layer Registration?

📂 `Api/Program.cs` — Lines 29-31:
```csharp
builder.Services.RegisterServiceModule(builder.Configuration);
builder.Services.RegisterInfrastructureModule(builder.Configuration);
builder.Services.RegisterSharedServiceModule(builder.Configuration);
```

- **Encapsulation** — setiap layer tahu apa yang dia perlu register
- **Maintainability** — tambah service baru? Ubah satu file je dalam layer tu
- **Clean Program.cs** — tak banjir dengan registrations

### Service Layer Registration

📂 `Service/ServiceCollectionExtensions.cs`:
```csharp
// Audit.NET — simpan audit ke file
Audit.Core.Configuration.DataProvider = new FileDataProvider(cfg => cfg.Directory("audit-logs"));

// IHttpContextAccessor — access HTTP context dari luar controller
services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Validation pipeline
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// BNM API config
services.Configure<BnmApiOptions>(configuration.GetSection("BnmApiSettings"));
```

### Shared Layer Registration

📂 `Shared/ServiceCollectionExtensions.cs`:
```csharp
// Audit dispatcher
services.AddScoped<IAuditLogEventDispatcher, AuditLogEventDispatcher>();

// BNM HTTP client dengan retry
services.AddHttpClient("BnmClient", ...).AddPolicyHandler(BuildRetryPolicy());

// Hangfire
services.AddHangfire(config => config.UseSqlServerStorage(...));
services.AddHangfireServer();
services.AddScoped<IExchangeRateSyncJob, ExchangeRateSyncJob>();
```

### Service Lifetimes

| Lifetime | Maksud | Contoh |
|----------|--------|--------|
| **Singleton** | 1 instance untuk keseluruhan app | `IHttpContextAccessor`, `IRateLimitConfiguration` |
| **Scoped** | 1 instance per HTTP request | `UnitOfWork`, `Repository`, `AuditLogEventDispatcher` |
| **Transient** | Instance baru setiap kali diperlukan | `RequestValidationBehavior` |

---

## 17 — Configuration (appsettings)

📂 `Api/appsettings.Development.json`

| Section | Tujuan | Contoh Value |
|---------|--------|-------------|
| `ConnectionStrings` | Database connection | `(localdb)\MSSQLLocalDB` |
| `BnmApiSettings.BaseUrl` | BNM API URL | `https://api.bnm.gov.my` |
| `BnmApiSettings.DefaultSession` | Default trading session | `1700` |
| `Serilog` | Log config | Level, WriteTo, Rolling |
| `ApiSecurity.ApiKey` | Development API key | `dev-unity-exchangerates-key-2026` |
| `CorsOptions.Origins` | Allowed domains | `#{CORS_ORIGINS}#` (CI/CD replace) |
| `IpRateLimitOptions` | Rate limit rules | 500 req/s per IP |

**Kenapa semua config-driven?** Tukar behavior tanpa recompile. Environment berbeza (dev/staging/prod) guna appsettings berbeza.

---

## 📋 Quick Reference — Semua NuGet Packages

| Package | Version | Kegunaan |
|---------|---------|---------|
| `Asp.Versioning.Mvc` | 8.1.0 | API versioning |
| `Asp.Versioning.Mvc.ApiExplorer` | 8.1.0 | Swagger version discovery |
| `AspNetCoreRateLimit` | 5.0.0 | Rate limiting |
| `Audit.NET` | 21.0.0 | Audit logging core |
| `Audit.WebApi.Core` | 21.0.0 | Audit middleware for ASP.NET |
| `Serilog` | - | Structured logging |
| `FluentValidation` | - | Input validation |
| `Mediator` | - | CQRS pattern |
| `Hangfire` | - | Background jobs |
| `Polly` | - | Retry/resilience |
| `AutoMapper` | - | Object mapping |

---

## 📋 Quick Reference — Middleware Pipeline Order

```
Request masuk
  → 1. ExceptionHandlerMiddleware     (catch semua exception)
  → 2. ApiKeyAuthMiddleware           (validate X-Api-Key)
  → 3. IpRateLimiting                 (check request quota)
  → 4. UseHttpsRedirection            (force HTTPS)
  → 5. UseCors                        (check allowed origins)
  → 6. UseAuditLog                    (capture request/response)
  → 7. UseAuthorization               (ASP.NET auth)
  → 8. Controller                     (handle request)
Response keluar ←
```
