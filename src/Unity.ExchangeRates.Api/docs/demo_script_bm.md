# Unity Exchange Rates API — Skrip Demo Detail (BM)

> **Cara guna skrip ni:** Baca setiap bahagian secara santai. 📂 = buka file ni dan tunjuk. Setiap code block ada explain line by line. Skrip ini lebih detail — sesuai untuk code review presentation.

---

## BAHAGIAN 1 — Gambaran Besar (2 min)

*"Hari ni saya nak walk through Unity Exchange Rates API yang saya dah bina. API ni serve satu tujuan utama — sediakan data kadar pertukaran mata wang untuk Life Asia."*

*"Setiap hari, dia auto fetch kadar terkini dari BNM Open API, simpan dalam database, dan expose endpoint untuk Life Asia query kadar tu."*

*"Ada 2 flow utama:*
1. *Automated daily job — fetch rates setiap tengah malam*
2. *REST API — user query kadar mengikut mata wang dan tarikh"*

*"Dan sebagai persediaan production, kita dah implement beberapa layer security — API versioning, API key authentication, rate limiting, audit logging, dan CORS control."*

---

## BAHAGIAN 2 — Struktur Projek (3 min)

📂 **Buka: Solution Explorer — tunjuk semua 6 projek**

*"Projek ni structured guna layered architecture pattern — sama macam Facility service."*

| Layer | Tanggungjawab |
|-------|--------------|
| **Api** | Entry point — controllers, middleware, security config, Swagger |
| **Domain** | Data models, entities, event contracts — pure C# class |
| **Repository** | Interface sahaja — kontrak untuk data access |
| **Infrastructure** | Implementation — EF Core, database context, repository code |
| **Service** | Business logic — CQRS handlers, validators, audit event handlers |
| **Shared** | Cross-cutting — Hangfire jobs, HTTP client, event dispatchers |

*"Prinsip utama: dependency direction. Inner layers macam Domain dan Repository takde dependency pada outer layers. Infrastructure implement Repository interfaces. Service ada business logic. Api ikat semuanya."*

---

## BAHAGIAN 3 — Program.cs: Entry Point (8 min)

📂 **Buka:** `Api/Program.cs` — 241 baris

### Line 1-14: Using Statements

```csharp
using AspNetCoreRateLimit;           // Line 1 — Package untuk rate limiting (hadkan bilangan request)
using Asp.Versioning;                // Line 2 — Package untuk API versioning (v1, v2, etc)
using Asp.Versioning.ApiExplorer;    // Line 3 — Bantu Swagger papar version
using Hangfire;                      // Line 4 — Background job scheduler
using Mediator;                      // Line 5 — CQRS mediator pattern
using Microsoft.OpenApi.Models;      // Line 6 — Swagger/OpenAPI models
using Serilog;                       // Line 7 — Structured logging library
using Unity.ExchangeRates.Infrastructure;   // Line 8 — Layer Infrastructure
using Unity.ExchangeRates.Service;          // Line 9 — Layer Service
using Unity.ExchangeRates.Shared;           // Line 10 — Layer Shared
using Unity.ExchangeRates.Shared.Jobs;      // Line 11 — Hangfire job classes
using Unity.ExchangeRates.Api.Configurations;       // Line 12 — Config classes (CORS, Audit, Mapper)
using Unity.ExchangeRates.Api.Configurations.Logging; // Line 13 — Serilog custom enricher
using Unity.ExchangeRates.Api.Middlewares;           // Line 14 — Middleware classes kita
```

*"Setiap `using` ni import satu namespace. Tengok — kita ada package untuk rate limiting, versioning, Hangfire, Mediator, Serilog, dan rujukan ke setiap layer dalam projek kita."*

### Line 19: Bootstrap

```csharp
var builder = WebApplication.CreateBuilder(args);  // Line 19
```

*"Ni buat satu `WebApplicationBuilder` — dia yang setup semua services sebelum app run."*

### Line 24: Serilog

```csharp
ConfigureLog(builder.Host);  // Line 24
```

*"Panggil function `ConfigureLog()` untuk setup Serilog. Kita tengok function dia nanti kat bawah."*

### Lines 29-31: Multi-Layer Service Registration

```csharp
builder.Services.RegisterServiceModule(builder.Configuration);          // Line 29
builder.Services.RegisterInfrastructureModule(builder.Configuration);   // Line 30
builder.Services.RegisterSharedServiceModule(builder.Configuration);    // Line 31
```

*"Ni key part — setiap layer register services dia sendiri:*
- *Line 29: **Service layer** — register Mediator pipeline, validators, Audit.NET data provider, IHttpContextAccessor*
- *Line 30: **Infrastructure** — register EF Core DbContext, repositories, UnitOfWork*
- *Line 31: **Shared** — register Hangfire, BNM HTTP client, AuditLogEventDispatcher"*

*"Kenapa buat macam ni? Supaya setiap layer manage DI registration dia sendiri. Kalau nak tambah service baru dalam Service layer, tak perlu sentuh Program.cs."*

### Lines 33-35: Mediator & AutoMapper

```csharp
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);  // Line 34
builder.Services.AddAutoMapper(typeof(Program).Assembly);                            // Line 35
```

*"Line 34 — Mediator kene register kat startup assembly sebab dia guna source generator. `Scoped` means satu instance per HTTP request.*

*Line 35 — AutoMapper scan assembly ni untuk cari semua mapping profiles."*

### Line 38: CORS

```csharp
ConfigureCors(builder.Environment, builder.Services, builder.Configuration);  // Line 38
```

*"Setup CORS — Cross-Origin Resource Sharing. Dalam development, allow semua origin. Dalam production, baca specific origins dari appsettings."*

### Lines 41-50: Controllers, Versioning, Rate Limiting, Swagger

```csharp
builder.Services.AddControllers();                                    // Line 41 — Register all controllers
ConfigureApiVersioning(builder.Services);                             // Line 44 — Setup API versioning
ConfigureRateLimit(builder.Services, builder.Configuration);          // Line 47 — Setup rate limiting
ConfigureSwagger(builder.Services);                                   // Line 50 — Setup Swagger dengan versioning + API key
```

*"4 benda register serentak — controllers, versioning, rate limiting, dan Swagger. Setiap satu ada function sendiri yang saya akan explain."*

### Line 55: Build

```csharp
var app = builder.Build();  // Line 55
```

*"Selepas semua services register, kita build application. Dari sini, kita setup middleware pipeline."*

### Lines 57-59: Middleware Pipeline

```csharp
app.UseMiddleware<ExceptionHandlerMiddleware>();  // Line 57
app.UseMiddleware<ApiKeyAuthMiddleware>();         // Line 58
app.UseIpRateLimiting();                          // Line 59
```

*"Ni sangat penting — middleware jalan ikut SUSUNAN. Request masuk dari atas ke bawah:*

1. *Line 57: **ExceptionHandlerMiddleware** — tangkap mana-mana exception yang tak dihandle, return JSON error yang proper. Ini paling luar sebab kalau API key middleware crash, exception handler masih boleh catch.*

2. *Line 58: **ApiKeyAuthMiddleware** — check header `X-Api-Key`. Kalau takde key atau key salah, terus reject 401. Request TAK sampai rate limiter atau controller.*

3. *Line 59: **IpRateLimiting** — check berapa banyak request dari IP ni. Kalau exceed limit (500/saat), reject 429."*

### Lines 61-76: Development-Only Middleware

```csharp
if (app.Environment.IsDevelopment())     // Line 61
{
    app.UseSwagger();                     // Line 63 — Enable Swagger JSON endpoint
    
    var apiVersionDescriptionProvider = app.Services
        .GetRequiredService<IApiVersionDescriptionProvider>();  // Line 65 — Get version provider
    
    app.UseSwaggerUI(options =>           // Line 66 — Setup Swagger UI
    {
        foreach (var description in apiVersionDescriptionProvider
            .ApiVersionDescriptions.Reverse())   // Line 68 — Loop setiap API version
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());  // Line 70-71 — Add endpoint per version
        }
    });

    app.UseHangfireDashboard();           // Line 75 — Hangfire dashboard (development only)
}
```

*"Swagger dan Hangfire dashboard hanya available dalam Development mode — tak nampak dalam production. Line 68 loop setiap API version dan buat Swagger doc untuk setiap satu. `Reverse()` supaya versi terbaru nampak dulu."*

### Lines 78-82: Standard Middleware

```csharp
app.UseHttpsRedirection();    // Line 78 — Force HTTPS
app.UseCors();                // Line 79 — Apply CORS policy
app.UseAuditLog();            // Line 80 — Audit logging middleware
app.UseAuthorization();       // Line 81 — ASP.NET authorization
app.MapControllers();         // Line 82 — Map controller routes
```

*"Line 80 penting — `UseAuditLog()` ni extension method yang kita buat sendiri. Dia capture setiap request dan response untuk audit trail. Saya explain class dia nanti."*

### Lines 87-91: Hangfire Recurring Job

```csharp
RecurringJob.AddOrUpdate<IExchangeRateSyncJob>(  // Line 87 — Register recurring job
    "daily-exchange-rate-sync",                   // Line 88 — Nama job
    job => job.SyncDailyAsync(CancellationToken.None),  // Line 89 — Method yang dipanggil
    "0 0 * * *",                                  // Line 90 — Cron: setiap hari jam 12AM
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });  // Line 91 — Ikut timezone server
```

*"Line 90: cron expression `0 0 * * *` = minit 0, jam 0, setiap hari, setiap bulan, setiap hari dalam minggu = **setiap hari pukul 12:00AM**. TimeZone set ke local supaya ikut masa server kita."*

### Lines 96-108: App Startup with Error Handling

```csharp
try {
    Log.Information("Exchange Rates API starting");  // Line 98 — Log startup
    app.Run();                                        // Line 99 — Start the server
}
catch (Exception ex) {
    Log.Fatal(ex, "Application terminated unexpectedly");  // Line 103 — Log fatal crash
}
finally {
    Log.CloseAndFlush();  // Line 107 — Pastikan semua log ditulis sebelum exit
}
```

*"Wrap dalam try-catch supaya kalau app crash waktu startup, kita masih boleh log error tu. `Log.CloseAndFlush()` pastikan buffer Serilog habis ditulis ke disk."*

### Lines 113-127: ConfigureApiVersioning()

📂 **Scroll ke function `ConfigureApiVersioning`**

```csharp
static void ConfigureApiVersioning(IServiceCollection services)  // Line 113
{
    services.AddApiVersioning(options =>       // Line 115 — Tambah versioning ke DI
    {
        options.AssumeDefaultVersionWhenUnspecified = true;  // Line 117 — Kalau client tak specify version, guna default
        options.DefaultApiVersion = new ApiVersion(1, 0);    // Line 118 — Default = v1.0
        options.ReportApiVersions = true;                    // Line 119 — Letak supported versions dalam response header
        options.ApiVersionReader = new UrlSegmentApiVersionReader();  // Line 120 — Baca version dari URL path
    })
    .AddApiExplorer(setup =>                  // Line 122 — API Explorer untuk Swagger
    {
        setup.GroupNameFormat = "'v'VVV";       // Line 124 — Format: v1, v2, etc
        setup.SubstituteApiVersionInUrl = true; // Line 125 — Replace {version} placeholder dalam route
    });
}
```

*"Line 117: `AssumeDefaultVersionWhenUnspecified` — kalau client call `/api/exchangerates` tanpa version, sistem assume v1.0. Ni untuk backward compatibility.*

*Line 120: `UrlSegmentApiVersionReader` — version dibaca dari URL path, bukan dari query string atau header. Contoh: `/api/v1/exchangerates`.*

*Line 125: `SubstituteApiVersionInUrl` — bila controller ada route `api/v{version:apiVersion}/exchangerates`, system auto replace `{version}` dengan version sebenar."*

### Lines 132-181: ConfigureSwagger()

📂 **Scroll ke function `ConfigureSwagger`**

```csharp
static void ConfigureSwagger(IServiceCollection services)  // Line 132
{
    services.AddEndpointsApiExplorer();  // Line 134 — Required untuk Swagger discover endpoints

    var serviceProvider = services.BuildServiceProvider();  // Line 136 — Build temporary provider
    var apiVersionDescriptionProvider = serviceProvider
        .GetRequiredService<IApiVersionDescriptionProvider>();  // Line 137 — Get version info

    services.AddSwaggerGen(swagger =>  // Line 139 — Configure Swagger generator
    {
        // Loop setiap API version dan buat Swagger doc
        foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)  // Line 141
        {
            var apiInfo = new OpenApiInfo   // Line 143
            {
                Title = "Unity Exchange Rates API",     // Line 145
                Version = $"{description.ApiVersion}"   // Line 146
            };
            if (description.IsDeprecated)  // Line 148 — Kalau version dah deprecated
            {
                apiInfo.Description += " This API version has been deprecated.";  // Line 150
            }
            swagger.SwaggerDoc(description.GroupName, apiInfo);  // Line 152 — Register doc
        }

        // Security Definition — buat button "Authorize" dalam Swagger UI
        swagger.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme  // Line 156
        {
            Description = "API Key authentication using the X-Api-Key header",  // Line 158
            Type = SecuritySchemeType.ApiKey,    // Line 159 — Jenis: API Key
            Name = "X-Api-Key",                 // Line 160 — Nama header
            In = ParameterLocation.Header,      // Line 161 — Location: header
            Scheme = "ApiKeyScheme"             // Line 162
        });

        // Security Requirement — force semua endpoint require API key
        swagger.AddSecurityRequirement(new OpenApiSecurityRequirement  // Line 165
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"            // Line 173 — Rujuk ke definition atas
                    },
                    In = ParameterLocation.Header
                },
                new List<string>()
            }
        });
    });
}
```

*"Line 141-152: Loop setiap version, buat separate Swagger doc. Sekarang kita ada V1 je. Kalau nanti tambah V2, Swagger auto papar kedua-dua.*

*Line 156-163: Ni yang buat button 🔒 Authorize dalam Swagger UI. User klik button tu, masukkan API key, dan semua request dari Swagger akan include header `X-Api-Key`.*

*Line 165-179: Security requirement — tanpa ni, Swagger tak send API key walaupun user dah authorize."*

### Lines 186-216: ConfigureCors()

```csharp
static void ConfigureCors(IWebHostEnvironment environment, IServiceCollection services, IConfiguration configuration)
{
    var corsOptions = configuration.GetSection(nameof(CorsOptions)).Get<CorsOptions>();  // Line 188
    if (corsOptions == null)     // Line 189
    {
        if (environment.IsDevelopment())  // Line 191 — Development mode
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin()    // Benarkan semua origin
                           .AllowAnyHeader()    // Benarkan semua header
                           .AllowAnyMethod();   // Benarkan semua HTTP method
                });
            });
        }
        else  // Production tapi CORS tak configured
        {
            throw new InvalidOperationException("Cors is not configured correctly in appsettings.");  // Line 203
        }
    }
    else  // Ada CorsOptions dalam config
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.WithOrigins(corsOptions.Origins)  // Line 212 — Hanya origin yang specified
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
            });
        });
    }
}
```

*"Line 188: Cuba baca `CorsOptions` dari appsettings. Kalau tak jumpa DAN kita dalam Development, allow semua (line 197). Kalau tak jumpa DAN bukan Development, throw error (line 203) — supaya production MESTI ada CORS configured.*

*Line 212: Dalam production, hanya origins yang listed dalam appsettings je boleh call API kita. `#{CORS_ORIGINS}#` dalam appsettings akan diganti oleh CI/CD pipeline dengan domain sebenar."*

### Lines 221-228: ConfigureRateLimit()

```csharp
static void ConfigureRateLimit(IServiceCollection services, IConfiguration configuration)
{
    services.AddMemoryCache();  // Line 223 — Rate limiter guna memory cache untuk track request count
    services.Configure<IpRateLimitOptions>(configuration.GetSection(nameof(IpRateLimitOptions)));  // Line 224
    services.Configure<IpRateLimitPolicies>(configuration.GetSection(nameof(IpRateLimitPolicies)));  // Line 225
    services.AddInMemoryRateLimiting();  // Line 226 — Guna in-memory counter
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();  // Line 227
}
```

*"Line 223: `AddMemoryCache()` — rate limiter perlukan cache untuk simpan berapa banyak request dari setiap IP.*

*Line 224-225: Baca config dari appsettings section `IpRateLimitOptions`. Config ni define limit (500 req/s), HTTP status code (429), dan custom response message.*

*Line 226: In-memory rate limiting — counter disimpan dalam RAM. Kalau app restart, counter reset. Untuk production boleh tukar ke distributed cache."*

### Lines 233-240: ConfigureLog()

```csharp
static void ConfigureLog(IHostBuilder hostBuilder)
{
    hostBuilder.UseSerilog((context, config) =>  // Line 235 — Ganti default logger dengan Serilog
    {
        config.ReadFrom.Configuration(context.Configuration);  // Line 237 — Baca Serilog config dari appsettings
        config.Enrich.WithMethodName();  // Line 238 — Tambah nama method dalam setiap log entry
    });
}
```

*"Line 237: Semua Serilog config (minimum level, WriteTo, output template) dibaca dari appsettings `Serilog` section. Maknanya boleh tukar log level tanpa recompile.*

*Line 238: `WithMethodName()` — custom enricher yang kita buat. Setiap log entry automatik letak nama method mana yang produce log tu."*

---

## BAHAGIAN 4 — API Key Authentication (5 min)

📂 **Buka:** `Api/Middlewares/ApiKeyAuthMiddleware.cs` — 71 baris

### Class Structure (Lines 6-18)

```csharp
public class ApiKeyAuthMiddleware  // Line 6
{
    private readonly RequestDelegate _next;                    // Line 8 — Delegate ke middleware seterusnya
    private readonly IConfiguration _configuration;            // Line 9 — Untuk baca API key dari config
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;    // Line 10 — Untuk log unauthorized attempts
    private const string ApiKeyHeaderName = "X-Api-Key";      // Line 11 — Nama header yang kita expect

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, 
                                ILogger<ApiKeyAuthMiddleware> logger)  // Line 13
    {
        _next = next;               // Line 15
        _configuration = configuration;  // Line 16
        _logger = logger;           // Line 17
    }
```

*"Line 8: `RequestDelegate _next` — ni reference ke middleware seterusnya dalam pipeline. Kalau kita tak panggil `_next(context)`, request berhenti sini.*

*Line 9: `IConfiguration` — untuk baca `ApiSecurity:ApiKey` dari appsettings.*

*Line 10: `ILogger` — typed logger supaya dalam log output, nama class `ApiKeyAuthMiddleware` akan nampak.*

*Line 11: `const string` — nama header tetap, semua client kene hantar header ni."*

### Invoke Method (Lines 20-51) — SETIAP REQUEST LALU SINI

```csharp
public async Task Invoke(HttpContext context)  // Line 20
{
    var path = context.Request.Path.Value?.ToLower() ?? string.Empty;  // Line 22

    // Skip Swagger dan Hangfire
    if (path.StartsWith("/swagger") || path.StartsWith("/hangfire"))  // Line 25
    {
        await _next(context);  // Line 27 — Teruskan tanpa check
        return;                // Line 28
    }
```

*"Line 22: Ambil path request dan lowercase kan. `?.` means kalau `Path.Value` null, guna empty string — elak NullReferenceException.*

*Line 25-28: Skip authentication untuk Swagger dan Hangfire dashboard. Sebab? Dua-dua ni development tools. Kalau kita require key, developer tak boleh buka Swagger. Dalam production, Swagger dah disabled (check Part 3 line 61)."*

```csharp
    // Check: Ada header X-Api-Key ke tak?
    if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))  // Line 31
    {
        _logger.LogWarning("API Key missing from request. Path={Path}, IP={IP}",
            context.Request.Path, context.Connection.RemoteIpAddress);  // Line 33-34

        await WriteUnauthorizedResponse(context, 
            "API Key is required. Provide it via the X-Api-Key header.");  // Line 36
        return;  // Line 37 — STOP! Tak panggil _next, request berhenti sini
    }
```

*"Line 31: `TryGetValue` — cuba ambil value dari header `X-Api-Key`. Kalau takde, masuk block ni.*

*Line 33-34: Log sebagai WARNING dengan path dan IP address — penting untuk security monitoring. Admin boleh monitor log untuk detect kalau ada orang cuba access tanpa key.*

*Line 36-37: Return 401 Unauthorized dan BERHENTI. `_next` tak dipanggil, maknanya request tak pernah sampai controller."*

```csharp
    // Check: Key yang dihantar betul ke tak?
    var configuredApiKey = _configuration["ApiSecurity:ApiKey"];  // Line 40
    if (string.IsNullOrEmpty(configuredApiKey) || 
        !string.Equals(extractedApiKey, configuredApiKey))  // Line 41
    {
        _logger.LogWarning("Invalid API Key provided. Path={Path}, IP={IP}",
            context.Request.Path, context.Connection.RemoteIpAddress);  // Line 43-44

        await WriteUnauthorizedResponse(context, "Invalid API Key.");  // Line 46
        return;  // Line 47
    }

    await _next(context);  // Line 50 — KEY BETUL! Teruskan ke middleware seterusnya
}
```

*"Line 40: Baca key yang betul dari config `ApiSecurity:ApiKey`.*

*Line 41: Dua check — (1) key dalam config tak kosong, DAN (2) key yang client hantar sama dengan key dalam config. Kalau tak match, reject.*

*Line 50: Ni paling penting — hanya sampai sini kalau key SAH. `_next(context)` teruskan request ke middleware seterusnya (rate limiter, kemudian controller)."*

### WriteUnauthorizedResponse (Lines 53-68)

```csharp
private static async Task WriteUnauthorizedResponse(HttpContext context, string message)  // Line 53
{
    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;  // Line 55 — 401
    context.Response.ContentType = "application/json";               // Line 56

    var response = new
    {
        status = "Failed",           // Line 60
        errorCode = "00401",         // Line 61
        errorMsg = message,          // Line 62
        timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz",
            System.Globalization.CultureInfo.InvariantCulture)  // Line 63-64
    };

    await context.Response.WriteAsync(JsonSerializer.Serialize(response));  // Line 67
}
```

*"Line 55: Set HTTP status ke 401 Unauthorized.*

*Line 58-65: Buat anonymous object dengan format JSON yang consistent dengan error response lain dalam API kita — ada status, errorCode, errorMsg, dan timestamp.*

*Line 67: Serialize ke JSON dan tulis ke response stream. Client dapat proper JSON error, bukan blank 401."*

---

## BAHAGIAN 5 — Controller (4 min)

📂 **Buka:** `Api/Controllers/ExchangeRateController.cs` — 57 baris

### Class Attributes (Lines 15-17)

```csharp
[ApiController]                                              // Line 15
[ApiVersion("1.0")]                                          // Line 16
[Route("api/v{version:apiVersion}/exchangerates")]           // Line 17
public class ExchangeRateController : BaseApiController      // Line 18
```

*"Line 15: `[ApiController]` — enable automatic model validation, binding source inference, dan problem details untuk errors.*

*Line 16: `[ApiVersion("1.0")]` — controller ni belong to version 1.0. Kalau nanti buat V2, boleh buat controller baru dengan `[ApiVersion("2.0")]`.*

*Line 17: Route template — `v{version:apiVersion}` diganti jadi `v1` secara automatik. URL sebenar jadi `/api/v1/exchangerates`.*

*Line 18: Inherit dari `BaseApiController` — base class yang ada method `ApiResponse()` untuk standardize response format."*

### Constructor & Dependencies (Lines 20-29)

```csharp
private readonly IMapper _mapper;                            // Line 20 — AutoMapper
private readonly ISender _mediator;                          // Line 21 — Mediator (hantar command/query)
private readonly ILogger<ExchangeRateController> _logger;    // Line 22 — Logger

public ExchangeRateController(IMapper mapper, ISender mediator, 
    ILogger<ExchangeRateController> logger)  // Line 24
{
    _mapper = mapper;       // Line 26
    _mediator = mediator;   // Line 27
    _logger = logger;       // Line 28
}
```

*"3 dependencies inject melalui constructor — standard DI pattern. `ISender` dari Mediator, bukan `IMediator`, sebab controller hanya perlu SEND (tak perlu publish notifications)."*

### GetRate Method (Lines 31-42) — GET Endpoint

```csharp
[HttpGet("{currency}/{date}")]                                      // Line 31
[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]  // Line 32
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]  // Line 33
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]    // Line 34
public async Task<IActionResult> GetRate(string currency, string date)  // Line 35
{
    _logger.LogInformation("GetRate request received: currency={currency}, date={date}", 
        currency, date);  // Line 37

    var request = new ExchangeRateRequest { currency = currency, date = date };  // Line 38
    var query = _mapper.Map<ExchangeRateQuery>(request);  // Line 39
    var result = await _mediator.Send(query);              // Line 40
    return ApiResponse<BaseResponse, BaseResult>(
        _mapper.Map<BaseResponse>(result.ValueOrDefault), result);  // Line 41
}
```

*"Line 31: `{currency}/{date}` jadi part URL. Contoh: `/api/v1/exchangerates/usd/2026-02-27`.*

*Line 32-34: `ProducesResponseType` — bagitahu Swagger response apa yang mungkin. Bantu documentation.*

*Line 37: Log setiap incoming request — structured logging dengan parameter `{currency}` dan `{date}`. Dalam production, admin boleh search log by currency.*

*Line 38: Buat request object dari URL parameters.*

*Line 39: AutoMapper convert `ExchangeRateRequest` → `ExchangeRateQuery`. Property names match, jadi auto-map.*

*Line 40: Hantar query melalui Mediator. Mediator cari handler yang handle `ExchangeRateQuery` dan execute dia.*

*Line 41: Map result ke response dan return. `ApiResponse` dari `BaseApiController` yang standardize JSON format."*

### Sync Method (Lines 44-54) — POST Endpoint

```csharp
[HttpPost("sync")]  // Line 44
public async Task<IActionResult> Sync([FromBody] ExchangeRateSyncRequest syncRequest)  // Line 48
{
    _logger.LogInformation("Sync request received: date={date}, session={session}", 
        syncRequest.date, syncRequest.session);  // Line 50
        
    var command = _mapper.Map<ExchangeRateSyncCommand>(syncRequest);  // Line 51
    var result = await _mediator.Send(command);  // Line 52
    return ApiResponse<BaseResponse, BaseResult>(
        _mapper.Map<BaseResponse>(result.ValueOrDefault), result);  // Line 53
}
```

*"Pattern yang sama — log, map, send, return. `[FromBody]` means data datang dari JSON body. Ni untuk manual sync — developer boleh trigger sync untuk mana-mana tarikh."*

---

## BAHAGIAN 6 — Audit Logs System (4 min)

📂 **Buka:** `Api/Configurations/AuditConfigurationBuilderExtensions.cs` — 27 baris

```csharp
public static class AuditConfigurationBuilderExtensions  // Line 5
{
    public static IApplicationBuilder UseAuditLog(this WebApplication builder)  // Line 7
    {
        builder.UseAuditMiddleware(_ => _                                 // Line 9
            .FilterByRequest(rq => !rq.Path.Value.EndsWith("favicon.ico"))  // Line 10
            .WithEventType("{verb}:{url}")                                // Line 11
            .IncludeHeaders()                                             // Line 12
            .IncludeResponseHeaders()                                     // Line 13
            .IncludeRequestBody()                                         // Line 14
            .IncludeResponseBody(ctx => ctx.Response.StatusCode != 200)); // Line 15

        builder.Use(async (context, next) => {  // Line 18
            context.Request.EnableBuffering();   // Line 19 — PENTING!
            await next();                        // Line 20
        });

        return builder;  // Line 23
    }
}
```

*"Line 7: Extension method — sebab tu dalam Program.cs boleh panggil `app.UseAuditLog()` secara direct.*

*Line 10: Filter out `favicon.ico` — browser auto-request ni, tak relevant untuk audit.*

*Line 11: Event type format `{verb}:{url}` — contoh: `GET:/api/v1/exchangerates/usd/2026-02-27`.*

*Line 12-13: Capture semua headers (request dan response) — termasuk `X-Api-Key`, `Content-Type`, etc.*

*Line 14: Capture request body — berguna untuk POST requests.*

*Line 15: Capture response body HANYA kalau bukan 200 — jimat storage, kalau 200 OK tak perlu simpan response body.*

*Line 19: `EnableBuffering()` — PENTING! Tanpa ni, request body hanya boleh dibaca sekali. Audit middleware baca sekali, controller baca sekali — dua kali. Buffering allow multiple reads."*

📂 **Buka:** `Domain/Events/IEvent.cs` → `IAuditLogEvent.cs` → `AuditLogEvent.cs`

*"Event contracts dalam Domain layer — sama macam Facility:*
- *`IEvent` extends `INotification` dari Mediator — base untuk semua domain events*
- *`IAuditLogEvent` define kontrak: `EventType`, `ReferenceId`, `Message`, `Data`*
- *`AuditLogEvent` implement interface tu. `EventType` auto-generate dari data type name."*

📂 **Buka:** `Service/EventHandlers/AuditLogEventHandler.cs`

```csharp
public async ValueTask Handle(AuditLogEvent notification, CancellationToken cancellationToken)
{
    using (var audit = await AuditScope.CreateAsync(notification.EventType, () => notification.Data))
    {
        audit.SetCustomField("ReferenceId", notification.ReferenceId);
        audit.SetCustomField("IpAddress", _httpContextAccessor.HttpContext?.Connection
            .RemoteIpAddress?.ToString() ?? "unknown");
        audit.Comment(notification.Message);
    }
}
```

*"Handler ni dipanggil oleh Mediator bila ada audit event. `AuditScope` tulis JSON file ke folder `audit-logs/` dengan semua info — event type, data, IP address, dan message."*

📂 **Buka:** `Service/ServiceCollectionExtensions.cs` — Lines 21-25

```csharp
Audit.Core.Configuration.DataProvider = new FileDataProvider(cfg => cfg.Directory("audit-logs"));  // Line 22
services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();  // Line 25
```

*"Line 22: Config Audit.NET simpan data sebagai JSON files dalam folder `audit-logs/`. Production boleh tukar ke database.*

*Line 25: `IHttpContextAccessor` register sebagai Singleton — dia provide access ke `HttpContext` dari luar controller (dalam handler kita)."*

📂 **Buka:** `Shared/ServiceCollectionExtensions.cs` — Line 17

```csharp
services.AddScoped<IAuditLogEventDispatcher, AuditLogEventDispatcher>();  // Line 17
```

*"Dispatcher registered sebagai Scoped — satu instance per request. Dia publish audit events melalui Mediator."*

---

## BAHAGIAN 7 — Business Logic: Sync Command Handler (5 min)

📂 **Buka:** `Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandHandler.cs`

*"Ni jantung aplikasi — semua business logic sync ada sini. Bila Hangfire atau manual POST trigger sync, handler ni yang jalan."*

*"Flow dia:*
1. *Parse tarikh input*
2. *`ResolveBusinessDate()` — kalau weekend, resolve ke Jumaat*
3. *Load semua currencies dari database*
4. *Begin database transaction*
5. *Loop setiap currency → call BNM API → parse response → simpan dalam database*
6. *Commit transaction kalau semua OK / Rollback kalau ada error"*

```csharp
private static DateTime ResolveBusinessDate(DateTime date)
{
    while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        date = date.AddDays(-1);
    return date;
}
```

*"`while` loop — selagi hari tu Saturday ATAU Sunday, tolak satu hari. Sabtu → Jumaat. Ahad → tolak satu jadi Sabtu → tolak lagi jadi Jumaat."*

| Job run | Yesterday | Resolve | Rate |
|---|---|---|---|
| Selasa 12AM | Isnin | Isnin ✅ | Isnin 1700 |
| Sabtu 12AM | Jumaat | Jumaat ✅ | Jumaat 1700 |
| Ahad 12AM | Sabtu → | **Jumaat** | Jumaat 1700 |
| Isnin 12AM | Ahad → | **Jumaat** | Jumaat 1700 |

---

## BAHAGIAN 8 — appsettings.Development.json (3 min)

📂 **Buka:** `Api/appsettings.Development.json`

*"Semua config yang drive behavior app ada sini."*

| Section | Tujuan |
|---------|--------|
| `ConnectionStrings` | Connection string ke LocalDB |
| `BnmApiSettings` | Base URL, endpoint, Accept header, default session (1700) |
| `Serilog` | Log level, output format, WriteTo (File + Console) |
| `ApiSecurity` | API key untuk development: `dev-unity-exchangerates-key-2026` |
| `CorsOptions` | Allowed origins — `#{CORS_ORIGINS}#` diganti oleh CI/CD |
| `IpRateLimitOptions` | Rate limit: 500 req/s, 429 response, custom JSON message |

---

## BAHAGIAN 9 — Live Demo (5 min)

### Demo 1: Swagger — Versioned + Secured
1. Buka Swagger → tunjuk dropdown **V1**
2. Tunjuk endpoints: `/api/v1/exchangerates/...`
3. Cuba GET **tanpa** key → tunjuk **401 Unauthorized** JSON
4. Click 🔒 **Authorize** → masuk key → cuba GET lagi → **200 OK**

### Demo 2: Manual Sync
1. POST `/api/v1/exchangerates/sync` dengan `{"date": "2026-02-25", "session": "1700"}`

### Demo 3: Hangfire
1. Buka `/hangfire` → tunjuk recurring job → tunjuk succeeded jobs

### Demo 4: Database
1. Tunjuk Currency table → ExchangeRateHistory table → weekend rows

### Demo 5: Audit Logs
1. Buka folder `audit-logs/` → tunjuk JSON audit file

---

## BAHAGIAN 10 — Rumusan (1 min)

| # | Feature | Status |
|---|---------|:---:|
| 1 | Fully automated — Hangfire sync daily | ✅ |
| 2 | API Key Authentication | ✅ |
| 3 | API Versioning (`/api/v1/`) | ✅ |
| 4 | Rate Limiting (500 req/s) | ✅ |
| 5 | Audit Logs (file-based) | ✅ |
| 6 | CORS Lock-down | ✅ |
| 7 | Resilient — retry policy + transaction rollback | ✅ |
| 8 | Clean architecture — 6 layers | ✅ |
| 9 | CQRS pattern + validation pipeline | ✅ |
| 10 | Structured logging (Serilog) | ✅ |
| 11 | Weekend-aware business logic | ✅ |
| 12 | JWT dari IDP | ⏳ Tunggu IDP |

---

## ⚡ Cheat Sheet Soalan

| Soalan | Jawapan |
|---|---|
| Kenapa tak call BNM real-time? | Performance — cache dalam DB, query instant. Kalau BNM down, data kita still ada. |
| Kalau job fail? | Ada POST endpoint untuk manual re-sync. Log tunjuk apa yang fail. |
| Kenapa guna API key bukan JWT? | Phase 1 — immediate protection. JWT akan ditambah bila IDP ready. |
| API key simpan mana production? | IDP / Azure Key Vault — bukan hardcoded. |
| Kalau rate limit exceeded? | 429 Too Many Requests dengan JSON message. |
| Audit log simpan mana? | File-based macam Facility. Boleh tukar ke database. |
| NuGet packages apa ditambah? | `Asp.Versioning.Mvc` 8.1.0, `AspNetCoreRateLimit` 5.0.0, `Audit.NET` 21.0.0, `Audit.WebApi.Core` 21.0.0 |
