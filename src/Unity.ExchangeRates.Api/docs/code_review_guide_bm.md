# Unity Exchange Rates API — Panduan Review Code (BM Detail)

> Dokumen ini explain setiap class yang terlibat dalam projek, **mengapa** kita guna setiap pattern/method, **kelebihan** dia, dan **line-by-line** code explanation. Disusun mengikut flow terbaik untuk code review presentation.

---

## DAFTAR KANDUNGAN

1. [Program.cs — Entry Point & Semua Configuration](#1-programcs)
2. [ExceptionHandlerMiddleware — Global Error Handling](#2-exceptionhandlermiddleware)
3. [ApiKeyAuthMiddleware — API Key Security](#3-apikeyauthmiddleware)
4. [ExchangeRateController — API Endpoints](#4-exchangeratecontroller)
5. [RequestValidationBehavior — Validation Pipeline](#5-requestvalidationbehavior)
6. [ExchangeRateSyncCommandValidator — Session Config Validation](#6-exchangeratesynccommandvalidator)
7. [ExchangeRateSyncCommandHandler — Core Business Logic](#7-exchangeratesynccommandhandler)
8. [ExchangeRateQueryHandler — Query Logic](#8-exchangeratequeryhandler)
9. [UnitOfWork — Transaction Management](#9-unitofwork)
10. [ExchangeRateRepository — Data Access](#10-exchangeraterepository)
11. [EntitySaveChangeInterceptor — Auto Audit Fields](#11-entitysavechangeinterceptor)
12. [ExchangeRateSyncJob — Hangfire Job](#12-exchangeratesyncjob)
13. [AuditConfigurationBuilderExtensions — Audit Logging](#13-auditconfigurationbuilderextensions)
14. [Audit Events (Domain → Service → Shared)](#14-audit-events)
15. [LogMethodNameEnricher — Custom Serilog Enricher](#15-logmethodnameenricher)
16. [ServiceCollectionExtensions — DI Registration](#16-servicecollectionextensions)

---

## 1. Program.cs

📂 **Path:** `src/Unity.ExchangeRates.Api/Program.cs` — 241 baris

**Apa benda ni?** Entry point untuk keseluruhan aplikasi. Semua services register sini, semua middleware configure sini.

**Kenapa penting?** Kalau tak faham file ni, tak faham macam mana app start dan flow request.

### Using Statements (Line 1-14)

```csharp
using AspNetCoreRateLimit;                              // Rate limiting package
using Asp.Versioning;                                   // API versioning
using Asp.Versioning.ApiExplorer;                       // Swagger version discovery
using Hangfire;                                         // Background job scheduler
using Mediator;                                         // CQRS mediator pattern
using Microsoft.OpenApi.Models;                         // Swagger/OpenAPI models
using Serilog;                                          // Structured logging
using Unity.ExchangeRates.Infrastructure;               // EF Core, Repositories
using Unity.ExchangeRates.Service;                      // Business logic handlers
using Unity.ExchangeRates.Shared;                       // Hangfire jobs, HTTP client
using Unity.ExchangeRates.Shared.Jobs;                  // Job interface
using Unity.ExchangeRates.Api.Configurations;           // CORS, Audit, Mapper config
using Unity.ExchangeRates.Api.Configurations.Logging;   // Serilog enricher
using Unity.ExchangeRates.Api.Middlewares;              // Exception handler, API Key auth
```

### Service Registration (Line 29-31)

```csharp
builder.Services.RegisterServiceModule(builder.Configuration);
builder.Services.RegisterInfrastructureModule(builder.Configuration);
builder.Services.RegisterSharedServiceModule(builder.Configuration);
```

**Kenapa pattern ni?** Setiap layer manage DI registration sendiri. Kelebihan:
- **Encapsulation** — layer Service tak perlu tahu internal Infrastructure
- **Maintainability** — nak tambah service baru? Ubah satu file je dalam layer tu
- **Test-friendly** — boleh mock satu layer penuh

### Middleware Pipeline (Line 57-59)

```csharp
app.UseMiddleware<ExceptionHandlerMiddleware>();   // 1st — catch semua exception
app.UseMiddleware<ApiKeyAuthMiddleware>();          // 2nd — validate API key
app.UseIpRateLimiting();                           // 3rd — check rate limit
```

**Kenapa susunan ni penting?**
- Exception handler MESTI paling luar — kalau API key middleware crash, exception handler masih catch
- API key check SEBELUM rate limiter — supaya request tanpa key tak kira dalam quota
- Rate limiter selepas auth — hanya authenticated requests yang dikira

### ConfigureApiVersioning (Line 113-127)

```csharp
static void ConfigureApiVersioning(IServiceCollection services)
{
    services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;   // Backward compatible
        options.DefaultApiVersion = new ApiVersion(1, 0);     // Default v1.0
        options.ReportApiVersions = true;                     // Header: api-supported-versions
        options.ApiVersionReader = new UrlSegmentApiVersionReader();  // Version dalam URL
    })
    .AddApiExplorer(setup =>
    {
        setup.GroupNameFormat = "'v'VVV";                // Format: v1, v2
        setup.SubstituteApiVersionInUrl = true;         // Auto replace {version} → 1
    });
}
```

**Kenapa guna URL Segment Versioning?**
- Clean URL: `/api/v1/exchangerates` vs `/api/exchangerates?api-version=1`
- Industry standard — sama macam yang Facility guna
- Senang faham — user nampak version terus dalam URL
- `ReportApiVersions = true` — response header tunjuk version mana yang supported

**Kenapa `AssumeDefaultVersionWhenUnspecified = true`?**
- Backward compatibility — client lama yang belum update masih boleh call tanpa version

### ConfigureSwagger (Line 132-181)

```csharp
// Loop setiap API version → buat Swagger doc untuk setiap satu
foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
{
    swagger.SwaggerDoc(description.GroupName, apiInfo);
}

// Security definition — buat button Authorize dalam Swagger UI
swagger.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
{
    Type = SecuritySchemeType.ApiKey,
    Name = "X-Api-Key",
    In = ParameterLocation.Header,
});

// Security requirement — force semua endpoint perlukan key
swagger.AddSecurityRequirement(...);
```

**Kenapa configure Swagger macam ni?**
- **Versioned docs** — kalau ada V1 dan V2, Swagger tunjuk dropdown untuk pilih version
- **Authorize button** — developer boleh test API dengan key terus dari Swagger, tak perlu Postman
- **Security requirement** — Swagger auto include `X-Api-Key` header dalam setiap request selepas authorize

### ConfigureRateLimit (Line 221-228)

```csharp
services.AddMemoryCache();                            // Counter simpan dalam RAM
services.Configure<IpRateLimitOptions>(...);           // Baca config dari appsettings
services.Configure<IpRateLimitPolicies>(...);          // Policy-based rules
services.AddInMemoryRateLimiting();                    // In-memory counter
services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
```

**Kenapa rate limiting penting?**
- **Prevent abuse** — kalau ada bot atau attacker spam API, rate limiter block dia
- **Protect resources** — database connection pool terhad, rate limiter jaga tak overflow
- **Fair usage** — semua client dapat fair share

**Config dalam appsettings:**
- `500 req/s` per IP — cukup untuk normal usage, block spam
- `429 Too Many Requests` — client tahu dia kena slow down
- Custom JSON response — bukan blank error page

### ConfigureCors (Line 186-216)

```csharp
if (corsOptions == null && environment.IsDevelopment())
    builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();  // Dev: allow semua
else if (corsOptions == null)
    throw new InvalidOperationException("Cors is not configured");  // Prod: WAJIB config
else
    builder.WithOrigins(corsOptions.Origins)...;  // Prod: hanya origins yang listed
```

**Kenapa ada throw exception?**
- **Safety net** — kalau deploy ke production tanpa CORS config, app tak start langsung
- Lebih baik gagal awal dari terdedah tanpa protection

### ConfigureLog (Line 233-240)

```csharp
hostBuilder.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);  // Config dari appsettings
    config.Enrich.WithMethodName();                        // Custom enricher
});
```

**Kenapa Serilog, bukan default logger?**
- **Structured logging** — log data sebagai key-value, bukan plain text. Boleh search/filter
- **Multiple sinks** — tulis ke File DAN Console serentak
- **Rolling interval** — file rotate setiap jam, avoid single huge file
- **Configuration-driven** — tukar log level tanpa recompile

---

## 2. ExceptionHandlerMiddleware

📂 **Path:** `src/Unity.ExchangeRates.Api/Middlewares/ExceptionHandlerMiddleware.cs`

**Apa benda ni?** Global exception handler — tangkap SEMUA exception yang tak dihandle.

**Kenapa guna middleware, bukan try-catch dalam setiap controller?**
- **DRY** — tulis error handling sekali, cover semua endpoint
- **Consistent response** — semua error return format JSON yang sama
- **Tak boleh miss** — walaupun developer lupa try-catch, middleware tetap catch

### Constructor Dependencies

```csharp
private readonly RequestDelegate _next;                          // Middleware seterusnya
private readonly IWebHostEnvironment _env;                       // Check environment (dev/prod)
private readonly ILogger<ExceptionHandlerMiddleware> _logger;    // Typed logger
```

### Invoke Method — Flow

```csharp
public async Task Invoke(HttpContext context)
{
    try
    {
        await _next(context);  // Cuba jalankan request ke middleware/controller seterusnya
    }
    catch (Exception error)
    {
        var response = context.Response;
        response.ContentType = "application/json";
        dynamic resultObject = new JObject();
        resultObject.message = new JValue(error?.Message);

        switch (error)
        {
            case ExchangeRatesDomainException:
                _logger.LogWarning(error, "Domain exception: {Message}", error?.Message);
                response.StatusCode = (int)HttpStatusCode.BadRequest;  // 400
                break;

            case ValidationException e:
                _logger.LogWarning(error, "Validation exception: {Message}", error?.Message);
                response.StatusCode = (int)HttpStatusCode.BadRequest;  // 400
                resultObject.message = e.Errors.Select(e => e.ErrorMessage).Distinct().FirstOrDefault();
                break;

            default:
                _logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;  // 500
                break;
        }

        await response.WriteAsync(resultObject.ToString());
    }
}
```

**Kenapa categorize exception?**
- **DomainException** → `LogWarning` + 400 — business rule violation, EXPECTED behavior
- **ValidationException** → `LogWarning` + 400 — input salah, EXPECTED behavior
- **Default** → `LogError` + 500 — unexpected crash, NEED ATTENTION

**Kenapa Warning untuk domain/validation, bukan Error?**
- Sebab ia bukan error sebenar — user hantar input tak valid, itu normal
- `LogError` reserved untuk benda yang BETUL-BETUL salah (database down, etc.)
- Dalam production, ops team monitor Error level — tak nak banjir dengan false alarm

---

## 3. ApiKeyAuthMiddleware

📂 **Path:** `src/Unity.ExchangeRates.Api/Middlewares/ApiKeyAuthMiddleware.cs` — 71 baris

**Apa benda ni?** Middleware yang validate `X-Api-Key` header setiap request.

**Kenapa guna Middleware, bukan Action Filter?**
- **Earlier in pipeline** — middleware run SEBELUM controller, filter run SELEPAS binding
- **Cover semua endpoint** — tak perlu letak attribute pada setiap controller
- **Boleh skip specific paths** — Swagger, Hangfire tak perlu auth

### Line-by-line Invoke

```csharp
var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
```
- `?.` = null-conditional — kalau `Path.Value` null, tak crash
- `?? string.Empty` = null-coalescing — kalau null, guna empty string
- `ToLower()` — case-insensitive comparison

```csharp
if (path.StartsWith("/swagger") || path.StartsWith("/hangfire"))
{
    await _next(context);  // Skip — allow tanpa key
    return;
}
```
- **Kenapa skip Swagger?** Swagger UI sendiri perlu load tanpa key. User authorize DALAM Swagger selepas UI load
- **Kenapa skip Hangfire?** Dashboard adalah dev tool. Dalam production, Swagger dan Hangfire dah disabled (line 61 Program.cs)

```csharp
if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
{
    _logger.LogWarning("API Key missing from request. Path={Path}, IP={IP}",
        context.Request.Path, context.Connection.RemoteIpAddress);
    await WriteUnauthorizedResponse(context, "API Key is required...");
    return;  // STOP — tak panggil _next
}
```
- `TryGetValue` — safe check, tak throw exception kalau header takde
- **Log IP address** — security audit trail. Kalau ada suspicious IP, boleh trace
- `return` tanpa `_next` — request BERHENTI sini, tak pernah sampai controller

```csharp
var configuredApiKey = _configuration["ApiSecurity:ApiKey"];
if (string.IsNullOrEmpty(configuredApiKey) || !string.Equals(extractedApiKey, configuredApiKey))
```
- Baca key dari config `ApiSecurity:ApiKey`
- `string.IsNullOrEmpty` check — kalau config kosong, reject juga (safety)
- `string.Equals` — exact string comparison

```csharp
await _next(context);  // Key SAH — teruskan ke rate limiter → controller
```
- Hanya sampai sini kalau key valid

### WriteUnauthorizedResponse

```csharp
context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;  // 401
context.Response.ContentType = "application/json";

var response = new
{
    status = "Failed",
    errorCode = "00401",
    errorMsg = message,
    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", ...)
};
```

**Kenapa proper JSON, bukan just status code?**
- Client developer nampak error message yang jelas
- Consistent format dengan error response lain dalam API
- `timestamp` — berguna untuk debugging ("bila masa error ni berlaku?")

---

## 4. ExchangeRateController

📂 **Path:** `src/Unity.ExchangeRates.Api/Controllers/ExchangeRateController.cs` — 57 baris

**Apa benda ni?** REST controller — bridge antara HTTP requests dan business logic.

**Kenapa controller SANGAT THIN?**
- **Single Responsibility** — controller hanya handle HTTP concerns (routing, status codes)
- **Business logic dalam handlers** — senang test tanpa HTTP context
- **CQRS pattern** — controller tak perlu tahu macam mana data di-process

### Class Attributes

```csharp
[ApiController]                                              // Enable auto model validation
[ApiVersion("1.0")]                                          // Version ni: v1.0
[Route("api/v{version:apiVersion}/exchangerates")]           // URL pattern
```

- `[ApiController]` — 3 automatic behaviors: (1) auto 400 untuk invalid model, (2) attribute routing required, (3) problem details for errors
- `[ApiVersion("1.0")]` — assign controller ke version 1.0. Nanti nak buat V2, boleh buat controller baru
- Route: `v{version:apiVersion}` → system replace jadi `v1`. URL jadi `/api/v1/exchangerates`

### Constructor (Line 24-29)

```csharp
private readonly IMapper _mapper;                            // AutoMapper
private readonly ISender _mediator;                          // Mediator (send only)
private readonly ILogger<ExchangeRateController> _logger;    // Typed logger

public ExchangeRateController(IMapper mapper, ISender mediator, ILogger<ExchangeRateController> logger)
```

**Kenapa `ISender` bukan `IMediator`?**
- `ISender` = hanya boleh `Send()` command/query
- `IMediator` = `Send()` + `Publish()` notifications
- Controller hanya perlu send, tak perlu publish — **Interface Segregation Principle**

**Kenapa `ILogger<ExchangeRateController>`?**
- Typed logger — dalam log output, nampak `ExchangeRateController` sebagai source
- Senang filter log by controller

### GetRate Method (Line 31-42)

```csharp
[HttpGet("{currency}/{date}")]
[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetRate(string currency, string date)
{
    _logger.LogInformation("GetRate request received: currency={currency}, date={date}",
        currency, date);

    var request = new ExchangeRateRequest { currency = currency, date = date };
    var query = _mapper.Map<ExchangeRateQuery>(request);
    var result = await _mediator.Send(query);
    return ApiResponse<BaseResponse, BaseResult>(_mapper.Map<BaseResponse>(result.ValueOrDefault), result);
}
```

**Line-by-line:**
1. `[HttpGet("{currency}/{date}")]` — URL: `/api/v1/exchangerates/usd/2026-02-27`
2. `[ProducesResponseType...]` — Swagger tahu response type, show dalam UI
3. `_logger.LogInformation(...)` — Log **setiap** request untuk monitoring. Structured format `{currency}`, `{date}` — boleh search by currency
4. `new ExchangeRateRequest{...}` — buat request object dari URL params
5. `_mapper.Map<ExchangeRateQuery>(request)` — AutoMapper convert Request → Query. Property names match, auto-map
6. `_mediator.Send(query)` — hantar ke handler. Mediator cari `ExchangeRateQueryHandler` dan execute
7. `ApiResponse<...>(...)` — method dari `BaseApiController` yang standardize response format

### Sync Method (Line 44-54)

```csharp
[HttpPost("sync")]
public async Task<IActionResult> Sync([FromBody] ExchangeRateSyncRequest syncRequest)
{
    _logger.LogInformation("Sync request received: date={date}, session={session}",
        syncRequest.date, syncRequest.session);

    var command = _mapper.Map<ExchangeRateSyncCommand>(syncRequest);
    var result = await _mediator.Send(command);
    return ApiResponse<BaseResponse, BaseResult>(...);
}
```

- `[FromBody]` — JSON body: `{"date": "2026-02-27", "session": "1700"}`
- `syncRequest.session` — session BNM: `0900`, `1130`, `1200`, `1700`
- Pattern sama — log, map, send, return. **Consistency** across all endpoints

---

## 5. RequestValidationBehavior

📂 **Path:** `src/Unity.ExchangeRates.Service/Behaviors/RequestValidationBehavior.cs`

**Apa benda ni?** Mediator pipeline behavior — macam middleware, tapi untuk Mediator.

**Kenapa guna pipeline behavior?**
- **Cross-cutting** — semua commands/queries auto-validated tanpa code duplicate
- **Fail fast** — kalau input tak valid, handler TAK dipanggil langsung
- **Separation** — validation logic terpisah dari business logic

```csharp
public class RequestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : ResultBase<TResponse>, new()
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<...> _logger;
```

- `IPipelineBehavior` — intercept setiap request SEBELUM handler
- `IEnumerable<IValidator<TRequest>>` — inject SEMUA validators untuk request type ni
- Generic `<TRequest, TResponse>` — satu behavior cover semua commands dan queries

```csharp
public async ValueTask<TResponse> Handle(TRequest message, 
    MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
{
    var context = new ValidationContext<TRequest>(message);
    var validationResults = await Task.WhenAll(
        _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
    var failures = validationResults
        .SelectMany(r => r.Errors)
        .Where(f => f != null).ToList();

    if (failures.Any())
    {
        _logger.LogWarning("Validation Error: {@errors}", errors);
        return new TResponse().WithErrors(errors);  // STOP — handler TAK dipanggil
    }

    return await next(message, cancellationToken);  // Valid — teruskan ke handler
}
```

**Line-by-line:**
1. `ValidationContext` — wrap request untuk FluentValidation
2. `Task.WhenAll` — run SEMUA validators **serentak** (parallel) — performance
3. `SelectMany` — flatten results dari semua validators jadi satu list
4. `failures.Any()` — kalau ada mana-mana error → return error, handler LANGSUNG tak dipanggil
5. `next(message, cancellationToken)` — hanya sampai sini kalau SEMUA validators pass

---

## 6. ExchangeRateSyncCommandValidator

📂 **Path:** `src/Unity.ExchangeRates.Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandValidator.cs`

**Apa benda ni?** Validator untuk POST sync endpoint — validate `date` dan `session`.

**Kenapa FluentValidation?**
- **Readable** — rule dibaca macam English: "RuleFor date, NotEmpty, Matches format"
- **Testable** — boleh unit test setiap rule secara terasing
- **Auto-discovery** — `AddValidatorsFromAssembly()` dalam DI auto-register semua validators

### Session Config — 0900, 1130, 1200, 1700

```csharp
private static readonly HashSet<string> ValidSessions = new() { "0900", "1130", "1200", "1700" };
```

**Kenapa 4 session ni?**
- Ini BNM trading sessions:
  - `0900` — Opening rate (pagi)
  - `1130` — Mid-morning rate
  - `1200` — Noon rate
  - `1700` — Closing rate (petang/akhir hari)
- Daily automated job guna `1700` (default dalam appsettings `BnmApiSettings.DefaultSession`)
- **Kenapa `1700`?** Sebab closing rate paling stabil — reflect pergerakan sepanjang hari
- `HashSet` bukan `List` — O(1) lookup vs O(n), lebih efficient untuk Contains check

```csharp
RuleFor(c => c.date)
    .NotEmpty()
    .WithErrorCode("00400")
    .WithMessage("Date is required.")
    .Matches(@"^\d{4}-\d{2}-\d{2}$")
    .WithErrorCode("00400")
    .WithMessage("Date must be in yyyy-MM-dd format.");
```

- `.NotEmpty()` — date wajib ada, tak boleh null/empty
- `.Matches(regex)` — MESTI format `yyyy-MM-dd` (contoh: `2026-02-27`)
- Custom `WithErrorCode("00400")` — consistent error code across API

```csharp
RuleFor(c => c.session)
    .Must(s => ValidSessions.Contains(s!))
    .When(c => !string.IsNullOrEmpty(c.session))
    .WithErrorCode("00400")
    .WithMessage("Session must be one of: 0900, 1130, 1200, 1700.");
```

- `.When(...)` — validation HANYA run kalau session provided. Kalau empty, default dari config
- `.Must(...)` — custom rule: session MESTI dalam list valid sessions
- **Kenapa conditional?** Sebab session is optional — kalau tak provide, handler guna default `1700` dari `BnmApiSettings.DefaultSession`

---

## 7. ExchangeRateSyncCommandHandler — Core Business Logic

📂 **Path:** `src/Unity.ExchangeRates.Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandHandler.cs`

**Apa benda ni?** JANTUNG aplikasi — semua sync logic ada sini.

### Constructor Dependencies

```csharp
private readonly IUnitOfWork _unitOfWork;                           // Transaction management
private readonly HttpClient _httpClient;                            // BNM API caller
private readonly BnmApiOptions _settings;                          // Config (URL, session, etc.)
private readonly ILogger<ExchangeRateSyncCommandHandler> _logger;  // Typed logger

public ExchangeRateSyncCommandHandler(
    IUnitOfWork unitOfWork,
    IHttpClientFactory httpClientFactory,
    IOptions<BnmApiOptions> settings,
    ILogger<ExchangeRateSyncCommandHandler> logger)
{
    _unitOfWork = unitOfWork;
    _httpClient = httpClientFactory.CreateClient("BnmClient");  // Named client
    _settings = settings.Value;
    _logger = logger;
}
```

**Kenapa `IHttpClientFactory` bukan `new HttpClient()`?**
- **Connection pool management** — factory reuse connections, avoid socket exhaustion
- **Named client** — `"BnmClient"` sudah pre-configured dengan BaseURL dan retry policy
- **IOptions pattern** — settings inject sebagai strongly-typed object, bukan raw string

### Handle Method — Step by Step

#### Step 1: Parse Date & Resolve Weekend

```csharp
var inputDate = DateTime.ParseExact(request.date!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
var targetDate = ResolveBusinessDate(inputDate);
var session = !string.IsNullOrEmpty(request.session) ? request.session : _settings.DefaultSession;
```

- `ParseExact` — strict parsing, hanya accept `yyyy-MM-dd`
- `ResolveBusinessDate()` — Sabtu/Ahad → Jumaat (BNM tak publish weekend)
- Session default ke `_settings.DefaultSession` (1700) kalau tak specified

```csharp
if (inputDate != targetDate)
    _logger.LogInformation("Input date {inputDate} falls on weekend, resolved to {targetDate}", ...);
```
- Log hanya kalau date berubah (weekend → Friday) — informational

#### Step 2: Load Currencies & Begin Transaction

```csharp
var currencies = await _unitOfWork.ExchangeRates.GetActiveCurrenciesAsync(cancellationToken);
await _unitOfWork.BeginTransactionAsync(cancellationToken);
```

**Kenapa transaction?**
- **All-or-nothing** — SEMUA currencies sync berjaya, ATAU semua rollback
- Takde situation "USD sync tapi GBP tak sync" — data integrity
- **UnitOfWork pattern** — handle transaction lifecycle (begin → commit/rollback)

#### Step 3: Loop Each Currency

```csharp
foreach (var curr in currencies)
{
    var url = $"{path}/{curr.Id}/date/{targetDateStr}?session={session}&quote=rm";
    var response = await _httpClient.GetAsync(url, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError("Failed to fetch {currency}. BNM API returned {StatusCode}", ...);
        continue;  // Skip, teruskan currency seterusnya
    }

    var bnmData = await response.Content.ReadFromJsonAsync<BnmApiResponse>(...);

    var history = new ExchangeRateHistory
    {
        CurrencyCode = curr.Id,
        RateDate = targetDate,
        BuyingRate = rateData.Rate?.BuyingRate ?? 0,
        SellingRate = rateData.Rate?.SellingRate ?? 0,
        MiddleRate = rateData.Rate?.MiddleRate ?? 0,
        CreatedBy = "System_Mediator"
    };

    await _unitOfWork.ExchangeRates.AddRateHistoryAsync(history, cancellationToken);
    syncedCount++;
}
```

**Kenapa `continue` bukan `throw`?**
- Kalau satu currency fail (misalnya BNM takde data JPY), yang lain masih boleh sync
- `LogError` record mana yang fail — boleh investigate kemudian
- **Partial sync lebih baik dari total failure**

**Kenapa `?? 0` (null coalescing)?**
- Safety — kalau BNM return null untuk rate, default ke 0 bukan crash

#### Step 4: Save & Commit

```csharp
await _unitOfWork.SaveChangesAsync(cancellationToken);
await _unitOfWork.CommitAsync(cancellationToken);

_logger.LogInformation("Completed. Synced {synced}/{total} currencies", syncedCount, currencies.Count);
```

#### Step 5: Error Handling

```csharp
catch (Exception ex)
{
    await _unitOfWork.RollbackAsync(cancellationToken);
    _logger.LogError(ex, "ExchangeRateSyncCommandHandler failed. Transaction rolled back.");
    return Result.Fail(new GeneralError() { errorCode = "00500", errorMsg = ex.Message });
}
```

**Kenapa rollback?**
- Database down, network error, etc. → rollback SEMUA data yang dah add
- Data integrity — TAKDE partial data dalam database

### ResolveBusinessDate

```csharp
private static DateTime ResolveBusinessDate(DateTime date)
{
    while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        date = date.AddDays(-1);
    return date;
}
```

- `static` — tak perlu instance state, pure function
- `while` loop — handle both Saturday DAN Sunday
- Saturday → tolak 1 → Friday ✅
- Sunday → tolak 1 → Saturday → tolak 1 → Friday ✅

---

## 8. ExchangeRateQueryHandler

📂 **Path:** `src/Unity.ExchangeRates.Service/Mediator/Queries/ExchangeRates/ExchangeRateQueryHandler.cs`

**Apa benda ni?** Handler untuk GET endpoint — query kadar dari database.

```csharp
var createdDate = DateTime.ParseExact(request.date!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
var history = await _repository.GetRateByCreatedDateAsync(request.currency!, createdDate, cancellationToken);

if (history is null)
{
    _logger.LogWarning("No rate found for currency={currency}, date={date}", ...);
    return Result.Fail(new NotFoundError() { errorCode = "00404", ... });
}

_logger.LogInformation("Success for currency={currency}, date={date}", ...);
return new BaseResult() { data = history };
```

**Kenapa query by `CreatedOn` bukan `RateDate`?**
- User tanya "kadar untuk hari ini" — bermaksud data yang di-sync hari ini
- `CreatedOn` = bila kita simpan data, `RateDate` = tarikh BNM
- Weekend: 3 rows boleh ada `RateDate` yang sama (Friday), tapi `CreatedOn` berbeza (Sat, Sun, Mon)

**Kenapa `LogWarning` untuk not found?**
- Kalau data takde, bermakna sync mungkin gagal — ops team perlu investigate
- Bukan `LogError` sebab mungkin memang belum sync (user query masa future date)

---

## 9. UnitOfWork

📂 **Path:** `src/Unity.ExchangeRates.Infrastructure/UnitOfWork.cs`

**Apa benda ni?** Wrap DbContext dan Repository dengan transaction management.

**Kenapa guna Unit of Work pattern?**
- **Single SaveChanges** — semua operations save sekali gus, bukan save satu-satu
- **Transaction control** — Begin → Commit/Rollback. Data integrity guaranteed
- **Testable** — mock `IUnitOfWork` dalam unit tests

```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private IDbContextTransaction? _transaction;   // Nullable — boleh ada atau takde

    public IExchangeRateRepository ExchangeRates { get; }  // Expose repository

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _logger.LogDebug("UnitOfWork: Transaction started");
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return;    // Guard — kalau takde transaction, skip
        await _transaction.CommitAsync(cancellationToken);
        _logger.LogDebug("UnitOfWork: Transaction committed");
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(cancellationToken);
        _logger.LogWarning("UnitOfWork: Transaction rolled back");  // Warning — something went wrong
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var count = await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("UnitOfWork: SaveChangesAsync persisted {Count} changes", count);
        return count;
    }
}
```

**Kenapa `Rollback` log sebagai Warning?**
- Rollback bermakna sesuatu tak kena — sync gagal, exception berlaku
- Ops team perlu tahu bila database rollback berlaku

**Kenapa `_transaction?` nullable?**
- Tak semua operations guna transaction (contoh: simple query)
- Guard `if (_transaction is null) return;` — elak NullReferenceException

---

## 10. ExchangeRateRepository

📂 **Path:** `src/Unity.ExchangeRates.Infrastructure/Repositories/ExchangeRateRepository.cs`

```csharp
public async Task<ExchangeRateHistory?> GetRateByCreatedDateAsync(
    string currencyCode, DateTime createdDate, CancellationToken cancellationToken)
{
    var history = await _context.ExchangeRateHistories
        .FirstOrDefaultAsync(h => h.CurrencyCode == currencyCode 
            && h.CreatedOn.Date == createdDate.Date, cancellationToken);
}
```

**Kenapa `CreatedOn.Date == createdDate.Date`?**
- `.Date` buang time component — compare DATE sahaja, ignore masa
- `CreatedOn` mungkin `2026-02-27 00:05:23`, tapi user query `2026-02-27` — kena match

**Kenapa setiap method ada logger?**
- **Debug level** — "method dipanggil" — untuk development troubleshooting
- **Information level** — "jumpa 8 currencies" — business milestones
- **Traceability** — boleh trace flow penuh dari controller → handler → repository

---

## 11. EntitySaveChangeInterceptor

📂 **Path:** `src/Unity.ExchangeRates.Infrastructure/Interceptors/EntitySaveChangeInterceptor.cs`

**Apa benda ni?** EF Core interceptor — auto-set `CreatedOn` dan `ModifiedOn` setiap kali save.

**Kenapa guna interceptor?**
- **DRY** — tak perlu manual `entity.CreatedOn = DateTime.Now` dalam setiap handler
- **Tak boleh miss** — setiap entity yang inherit `BaseEntity` auto-populated
- **Audit trail** — setiap record ada timestamp bila created dan modified

```csharp
private void UpdateEntities(DbContext? context)
{
    foreach (var entry in context.ChangeTracker.Entries<BaseEntity<int>>())
    {
        if (entry.State == EntityState.Added)
            entry.Entity.CreatedOn = DateTime.Now;     // New record

        if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            entry.Entity.ModifiedOn = DateTime.Now;    // New or updated
    }
}
```

- `ChangeTracker.Entries<BaseEntity<int>>()` — scan SEMUA entities yang sedang di-track
- `EntityState.Added` — baru masuk → set `CreatedOn` DAN `ModifiedOn`
- `EntityState.Modified` — update existing → set `ModifiedOn` sahaja

---

## 12. ExchangeRateSyncJob

📂 **Path:** `src/Unity.ExchangeRates.Shared/Jobs/ExchangeRateSyncJob.cs`

**Apa benda ni?** Hangfire background job — trigger setiap hari 12AM.

```csharp
public async Task SyncDailyAsync(CancellationToken cancellationToken = default)
{
    try
    {
        var now = DateTime.Now;
        var yesterday = now.Date.AddDays(-1).ToString("yyyy-MM-dd");

        _logger.LogInformation("Hangfire SyncDaily: Starting. Now={Now}, TargetDate={Target}", now, yesterday);

        var command = new ExchangeRateSyncCommand { date = yesterday };
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailed)
            _logger.LogError("Sync failed for {Target}. Errors={Errors}", yesterday, ...);
        else
            _logger.LogInformation("Sync succeeded for {Target}.", yesterday);
    }
    catch (Exception ex)
    {
        _logger.LogCritical(ex, "Hangfire SyncDaily: Job crashed unexpectedly");
    }
}
```

**Kenapa yesterday?** Job run 12AM → BNM dah publish rate 5PM semalam → kita ambil semalam punya

**Kenapa `LogCritical`?** Job crash = data TAKDE untuk hari tu = production issue. `Critical` = highest severity

**Kenapa guna Mediator, bukan direct call?** Reuse exact same handler — zero code duplication. POST endpoint dan Hangfire job guna handler yang sama.

---

## 13. AuditConfigurationBuilderExtensions

📂 **Path:** `src/Unity.ExchangeRates.Api/Configurations/AuditConfigurationBuilderExtensions.cs`

Sudah detail dalam Bahagian 6 di atas. Extension method untuk `UseAuditLog()` yang capture request/response untuk audit trail menggunakan `Audit.WebApi` library.

---

## 14. Audit Events

Sistem audit event menggunakan event-driven pattern melalui Mediator:

| # | File | Path | Fungsi |
|---|------|------|--------|
| 1 | `IEvent.cs` | `Domain/Events/` | Base interface — extends Mediator's `INotification` |
| 2 | `IAuditLogEvent.cs` | `Domain/Events/` | Contract: `EventType`, `ReferenceId`, `Message`, `Data` |
| 3 | `AuditLogEvent.cs` | `Domain/Events/` | Concrete class. `EventType` auto dari `Data.GetType().Name` |
| 4 | `IAuditLogEventDispatcher.cs` | `Service/Services/` | Dispatcher interface |
| 5 | `AuditLogEventHandler.cs` | `Service/EventHandlers/` | Handle event → buat `AuditScope` → tulis ke file |
| 6 | `AuditLogEventDispatcher.cs` | `Shared/Services/` | Publish event via Mediator |

**Kenapa pattern ni? Kenapa tak direct write?**
- **Decoupled** — business code tak tahu macam mana audit ditulis (file? database? cloud?)
- **Extensible** — nak tukar dari file ke database? Tukar `DataProvider` je, handler sama
- **Testable** — mock dispatcher dalam tests
- **Same pattern as Facility** — consistent across projek

---

## 15. LogMethodNameEnricher

📂 **Path:** `src/Unity.ExchangeRates.Api/Configurations/Logging/LogMethodNameEnricher.cs`

**Apa benda ni?** Custom Serilog enricher — tambah nama method dalam setiap log entry automatik.

**Kenapa buat custom enricher?**
- Default Serilog hanya log `SourceContext` (class name)
- Dengan enricher ni, setiap log ada method name — senang trace "log ni datang dari function mana"

**Didaftarkan dalam** `ConfigureLog()` di `Program.cs`:
```csharp
config.Enrich.WithMethodName();
```

---

## 16. ServiceCollectionExtensions — DI Registration

### Service Layer

📂 **Path:** `src/Unity.ExchangeRates.Service/ServiceCollectionExtensions.cs`

```csharp
// Audit.NET data provider — tulis audit logs ke folder
Audit.Core.Configuration.DataProvider = new FileDataProvider(cfg => cfg.Directory("audit-logs"));

// IHttpContextAccessor — access HttpContext dari luar controller
services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// FluentValidation — cascade mode Stop (first error stops)
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

// Mediator pipeline behavior — validation behavior
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));

// Auto-discover dan register SEMUA validators dalam assembly
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// BNM API settings — bind dari appsettings ke strongly-typed class
services.Configure<BnmApiOptions>(configuration.GetSection("BnmApiSettings"));
```

### Shared Layer

📂 **Path:** `src/Unity.ExchangeRates.Shared/ServiceCollectionExtensions.cs`

```csharp
// Audit dispatcher — publish events through Mediator
services.AddScoped<IAuditLogEventDispatcher, AuditLogEventDispatcher>();

// BNM HTTP client — named client dengan retry policy
services.AddHttpClient("BnmClient", ...)
    .AddPolicyHandler(BuildRetryPolicy());  // Polly: 3 retries (1s, 2s, 5s)

// Hangfire — background job processing
services.AddHangfire(config => config.UseSqlServerStorage(...));
services.AddHangfireServer();
services.AddScoped<IExchangeRateSyncJob, ExchangeRateSyncJob>();
```

**Kenapa `AddScoped` untuk dispatcher dan job?**
- Scoped = satu instance per HTTP request / per Hangfire job execution
- Setiap request dapat fresh instance — tak share state antara requests

**Kenapa Polly retry?**
- BNM API boleh down sementara (maintenance, network issue)
- Retry 3 kali: tunggu 1s → 2s → 5s sebelum give up
- **Resilience** — kalau BNM down 2 saat, retry auto cover tanpa manual intervention
