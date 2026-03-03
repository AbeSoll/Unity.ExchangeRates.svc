# Unity Exchange Rates API — Latest Updates Demo Script

> Casual presentation script for code review with line manager and leader.
> Covers: **Security**, **API Versioning**, **GET Exchange Rates (optional filter)**, **GET Currencies**, and **Database Schema (CurrencyId)**.

---

## 🎯 What We'll Cover Today

| # | Topic | Status |
|---|-------|--------|
| 1 | [Security — Rate Limiting, CORS, Audit Logging](#1--security) | ✅ Done |
| 2 | [API Versioning — V1 Setup & Future V2 Guide](#2--api-versioning) | ✅ Done |
| 3 | [GET Exchange Rates — Optional Currency & Date Filter](#3--get-exchange-rates) | ✅ Done |
| 4 | [GET Currencies Endpoint](#4--get-currencies-endpoint) | ✅ Done |
| 5 | [Database Schema — CurrencyId as Primary Key](#5--database-schema--currencyid) | ✅ Done |

---

## 1 — Security

> "For security, we've implemented three layers of protection — Rate Limiting, CORS Lock-down, and Audit Logging. Authentication (JWT/IDP) will be added in a future phase."

---

### 1.1 Rate Limiting

> "Rate limiting prevents any single IP from flooding our API. If someone tries to spam hundreds of requests per second, we block them with a 429 status code."

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Program.cs` — `ConfigureRateLimit()` method

```csharp
static void ConfigureRateLimit(IServiceCollection services, IConfiguration configuration)
{
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(
        configuration.GetSection(nameof(IpRateLimitOptions)));
    services.Configure<IpRateLimitPolicies>(
        configuration.GetSection(nameof(IpRateLimitPolicies)));
    services.AddInMemoryRateLimiting();
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

> "We allow 500 requests per second per IP. That's plenty for normal usage, but it blocks automated abuse. The response is proper JSON — not a blank error page."

**Why this matters:**
- Protects database connection pool from being exhausted
- Prevents denial-of-service from a single source
- Config-driven — can tighten limits in production without redeploying

---

### 1.2 CORS Lock-down

> "CORS controls which domains can call our API from a browser. In dev mode, we allow everything. In production, only listed origins are allowed."

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Program.cs` — `ConfigureCors()` method

> "There's a safety net — if someone tries to deploy to production without configuring CORS, the app throws an exception and won't start. Better to fail loudly than to run wide open."

---

### 1.3 Audit Logging

> "Every API request is automatically captured as an audit trail — who called it, when, what they sent, and what we returned. This is for compliance and debugging."

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Configurations/AuditConfigurationBuilderExtensions.cs`

**Key design decisions:**
- Skip `favicon.ico` — no point logging browser icon requests
- Capture response body only for errors (not 200 OK) — saves storage
- `EnableBuffering()` — allows request body to be read twice (audit + controller)
- Event-driven architecture with Mediator-based dispatcher
- File-based storage (config-driven path) — easy to switch to DB later

---

## 2 — API Versioning

> "We implemented URL segment versioning. Every endpoint has the version in the URL path — `/api/v1/exchange-rates/...`."

---

### 2.1 How It's Configured

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Program.cs` — `ConfigureApiVersioning()` method

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

> "Key points:
> - `AssumeDefaultVersionWhenUnspecified` — if an old client calls without a version, it defaults to v1
> - `UrlSegmentApiVersionReader` — version is read from the URL path itself
> - `ReportApiVersions` — response includes `api-supported-versions` header"

📂 **Controller:** `src/Unity.ExchangeRates.Api/Controllers/ExchangeRateController.cs`

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/exchange-rates")]
public class ExchangeRateController : BaseApiController
```

---

### 2.2 How to Add V2 Later (Guide)

> "When V2 is needed, it's a 4-step process:"

#### Step 1 — Create a New V2 Controller

```csharp
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/exchange-rates")]
public class ExchangeRateV2Controller : BaseApiController
{
    // V2: different response format, additional fields, etc.
}
```

> "V1 controller stays EXACTLY as is. We create a brand new controller."

#### Step 2 — Both Versions Run Simultaneously

```
/api/v1/exchange-rates?currency=usd    ← V1 clients still work
/api/v2/exchange-rates?currency=usd    ← V2 clients use the new format
```

#### Step 3 — Swagger Auto Shows Both

> "Since our `ConfigureSwagger()` loops all versions, the Swagger UI automatically shows a dropdown with `v1` and `v2`."

#### Step 4 — Deprecate V1 (Eventually)

```csharp
[ApiVersion("1.0", Deprecated = true)]   // Add this to V1 controller
```

> "V1 still works — response header shows `api-deprecated-versions: 1.0`, and Swagger marks it as deprecated."

---

## 3 — GET Exchange Rates

> "The GET endpoint now supports optional filtering. You can get all rates or filter by currency and/or date."

---

### 3.1 Endpoint Usage

**URL:** `GET /api/v1/exchange-rates`

| Request | What You Get |
|---------|-------------|
| `GET /api/v1/exchange-rates` | All rates for today |
| `GET /api/v1/exchange-rates?currency=usd` | USD rate for today |
| `GET /api/v1/exchange-rates?date=2026-03-03` | All rates for 3 March |
| `GET /api/v1/exchange-rates?currency=usd&date=2026-03-03` | USD rate for 3 March |

> "Both `currency` and `date` are optional query parameters. If `date` is empty, it defaults to today (UTC)."

---

### 3.2 Controller

📂 **Where to look:** `src/Unity.ExchangeRates.Api/Controllers/ExchangeRateController.cs`

```csharp
[HttpGet]
[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetRate([FromQuery] string? currency, [FromQuery] string? date)
{
    if (string.IsNullOrEmpty(date))
    {
        date = DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    _logger.LogInformation("GetRate request received: currency={currency}, date={date}", currency ?? "ALL", date);
    var request = new ExchangeRateRequest { currency = currency, date = date };
    var query = _mapper.Map<ExchangeRateQuery>(request);
    var result = await _mediator.Send(query);
    return ApiResponse<BaseResponse, BaseResult>(_mapper.Map<BaseResponse>(result.ValueOrDefault), result);
}
```

> "Both parameters use `[FromQuery]` — they come from the URL query string, not the path."

---

### 3.3 Handler Logic (Branching)

📂 **Where to look:** `src/Unity.ExchangeRates.Service/Mediator/Queries/ExchangeRates/ExchangeRateQueryHandler.cs`

```csharp
public async ValueTask<Result<BaseResult>> Handle(ExchangeRateQuery request, CancellationToken cancellationToken)
{
    var createdDate = DateTime.ParseExact(request.date!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // If no currency specified — return ALL rates for the date
    if (string.IsNullOrEmpty(request.currency))
    {
        var histories = await _repository.GetAllRatesByDateAsync(createdDate, cancellationToken);
        // ... return list
        return new BaseResult() { data = histories };
    }

    // Single currency
    var history = await _repository.GetRateByCreatedDateAsync(request.currency, createdDate, cancellationToken);
    // ... return single
    return new BaseResult() { data = history };
}
```

> "Same handler, same query class. It branches based on whether `currency` is provided. This avoids duplicating query/handler classes."

---

### 3.4 Validation

📂 **Where to look:** `src/Unity.ExchangeRates.Service/Mediator/Queries/ExchangeRates/ExchangeRateQueryValidator.cs`

```csharp
public ExchangeRateQueryValidator()
{
    // Currency is optional — no validation rule

    RuleFor(c => c.date)
        .NotEmpty().WithMessage("Date is required.")
        .Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("Date must be in yyyy-MM-dd format.");
}
```

> "`currency` validation was removed — it's now optional. `date` is still validated (required + format check)."

---

## 4 — GET Currencies Endpoint

> "Lists all available currencies from the database. Useful for clients to know which currencies they can query."

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

> "Follows the same CQRS pattern — query, send through Mediator, return result. The controller is thin — 4 lines of logic."

---

## 5 — Database Schema — CurrencyId

> "We updated the Currency table to use `CurrencyId` (auto-increment integer) as the primary key. Previously, `CurrencyCode` (string) was the PK."

### Current Table Structure

```
Currency
├── CurrencyId (int, PK, auto-increment)
├── CurrencyCode (nvarchar(10), unique index)
├── CurrencyName (nvarchar(100))
├── UnitBase (int)
├── CreatedOn, CreatedBy, ModifiedOn, ModifiedBy, IsDeleted

ExchangeRateHistory
├── Id (int, PK, auto-increment)
├── CurrencyId (int, FK → Currency.CurrencyId)
├── CurrencyCode (nvarchar, kept for BNM API reference)
├── RateDate, BuyingRate, SellingRate, MiddleRate
├── CreatedOn, CreatedBy, ModifiedOn, ModifiedBy, IsDeleted
```

> "Key changes:
> - `CurrencyId` is now the integer PK (auto-increment)
> - `CurrencyCode` remains as a unique indexed column
> - `ExchangeRateHistory.CurrencyId` is the FK referencing `Currency.CurrencyId`
> - `CurrencyCode` is kept in `ExchangeRateHistory` for BNM API reference and logging"

---

## 📋 Quick Reference

### Endpoint Inventory

| Method | URL | Purpose |
|--------|-----|---------|
| `GET` | `/api/v1/exchange-rates` | Get exchange rates (optional `?currency=` & `?date=`) |
| `GET` | `/api/v1/exchange-rates/currencies` | List all available currencies |
| `POST` | `/api/v1/exchange-rates/sync` | Sync rates from BNM API |

### Middleware Pipeline

```
Request comes in
  → 1. ExceptionHandlerMiddleware     (catches all unhandled exceptions)
  → 2. IpRateLimiting                 (checks request quota per IP)
  → 3. CORS                           (checks allowed origins)
  → 4. Audit Logging                  (captures request/response)
  → 5. Controller                     (handles the request)
Response goes out ←
```

> _Note: Authentication (JWT/IDP) will be added in a future phase when IDP is ready._
