# Unity Exchange Rates API — Full Demo Script

> **Audience:** Leader & Manager  
> **Duration:** ~15–20 minutes  
> **Tools:** Postman (pre-configured GET endpoints), Visual Studio / VS Code, SQL Server Management Studio (SSMS)

---

## Table of Contents

1. [Pre-Demo Checklist](#1-pre-demo-checklist)
2. [Demo Flow Overview](#2-demo-flow-overview)
3. [Part A — API Walkthrough (Postman)](#3-part-a--api-walkthrough-postman)
4. [Part B — Security Deep-Dive](#4-part-b--security-deep-dive)
5. [Part C — Audit Log Explanation](#5-part-c--audit-log-explanation)
6. [Part D — Application Log Explanation](#6-part-d--application-log-explanation)
7. [Part E — Program.cs Walkthrough](#7-part-e--programcs-walkthrough)
8. [Part F — Audit Log Middleware Code Walkthrough](#8-part-f--audit-log-middleware-code-walkthrough)
9. [Closing Summary](#9-closing-summary)

---

## 1. Pre-Demo Checklist

| # | Action | Status |
|---|--------|--------|
| 1 | API running locally (`dotnet run`) | ☐ |
| 2 | Database has synced exchange rate data | ☐ |
| 3 | Postman collection ready with GET endpoints | ☐ |
| 4 | SSMS open and connected to `UnityExchangeRatesDb` | ☐ |
| 5 | Source code open at `Program.cs` and `AuditLogMiddleware.cs` | ☐ |

---

## 2. Demo Flow Overview

```
┌──────────────────────────────────────────────────────────┐
│  Step 1: Hit APIs in Postman (2 min)                     │
│  Step 2: Show security implementations (3 min)           │
│  Step 3: Show Audit Log in DB + explain columns (3 min)  │
│  Step 4: Show Application Log in DB + console (3 min)    │
│  Step 5: Walk through Program.cs (3 min)                 │
│  Step 6: Walk through AuditLogMiddleware.cs (3 min)      │
│  Step 7: Q&A                                             │
└──────────────────────────────────────────────────────────┘
```

---

## 3. Part A — API Walkthrough (Postman)

### Script

> **SAY:** "Let me start by showing the API is live and working. We have two GET endpoints in version 1."

### Step A1 — Get All Exchange Rates (latest date)

**Postman Request:**
```
GET {{baseUrl}}/api/v1/exchange-rates
```

> **SAY:** "This returns all currency exchange rates for the latest available date. No parameters needed — the system auto-resolves to the most recent date in the database."

**Point out in response:**
- `appId`: Identifies the service in a microservice ecosystem
- `status`: "Success"
- `traceId`: Distributed tracing correlation ID
- `timestamp`: ISO 8601 format with timezone offset
- `data`: Array of rate objects with `currencyCode`, `unit`, `rate`, `session`, `lastUpdatedAt`, `source`

### Step A2 — Get Exchange Rate by Currency + Date

**Postman Request:**
```
GET {{baseUrl}}/api/v1/exchange-rates?currency=USD&date=2025-03-11
```

> **SAY:** "Here we query a specific currency on a specific date. The system returns the latest BNM session available for that currency on that date."

### Step A3 — Trigger Validation Error

**Postman Request:**
```
GET {{baseUrl}}/api/v1/exchange-rates?date=11-03-2025
```

> **SAY:** "If we pass an invalid date format, the FluentValidation pipeline catches it before hitting the database. We get a 400 with a clear error message and trace ID."

**Point out in response:**
- `status`: "Failed"
- `errorCode`: "00400"
- `errorMsg`: "Date must be in yyyy-MM-dd format."

### Step A4 — Get All Currencies

**Postman Request:**
```
GET {{baseUrl}}/api/v1/exchange-rates/currencies
```

> **SAY:** "This returns the master list of all currencies we track from BNM."

### Step A5 — Trigger 404 (No Data)

**Postman Request:**
```
GET {{baseUrl}}/api/v1/exchange-rates?currency=XYZ&date=2025-01-01
```

> **SAY:** "Querying a non-existent currency or a date with no data returns a clean 404 with a trace ID for debugging."

---

## 4. Part B — Security Deep-Dive

> **SAY:** "Now let me show the security measures we've implemented. This is important because even an internal API should follow defense-in-depth."

### Step B1 — Show Security Response Headers (Postman)

Go to the **Headers** tab on any Postman response.

> **SAY:** "Look at these response headers. Let me explain each one:"

| Header | Value | Explanation |
|--------|-------|-------------|
| `X-Content-Type-Options` | `nosniff` | "Prevents browsers from MIME-sniffing the response away from the declared Content-Type. Blocks XSS attacks via file type confusion." |
| `X-Frame-Options` | `DENY` | "Prevents this API's responses from being embedded in an iframe. This mitigates clickjacking attacks." |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | "Controls how much referrer information is shared when navigating away. Prevents leaking internal URLs to external sites." |
| `Content-Security-Policy` | `default-src 'none'` | "Tells browsers to not load any resources (scripts, styles, images) from any source. Since this is a JSON API — no resources should ever load." |
| Server header | **ABSENT** | "We suppressed the `Server: Kestrel` header. This avoids server fingerprinting — attackers can't identify our web server technology." |

**Show the code in Program.cs (line ~20):**
```csharp
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
```

**Show the middleware inline (line ~63–75):**
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    // ... etc
    await next();
});
```

### Step B2 — Rate Limiting

> **SAY:** "We implement IP-based rate limiting to prevent abuse."

**Show in Postman:** Make rapid requests — after 60 requests in 1 minute, you get HTTP 429.

**Show config in appsettings.json:**
```json
"IpRateLimitOptions": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "HttpStatusCode": 429,
    "GeneralRules": [
        { "Endpoint": "*", "Period": "1m", "Limit": 60 },
        { "Endpoint": "post:/api/*/exchange-rates/sync", "Period": "1m", "Limit": 5 }
    ]
}
```

> **SAY:** "General limit is 60 requests per minute per IP. The sync endpoint has a stricter limit of 5 per minute because it writes to the database and calls external BNM API."

### Step B3 — HTTPS Redirection

> **SAY:** "We enforce HTTPS redirection. Any HTTP request is automatically redirected to HTTPS." 

Show in Program.cs:
```csharp
app.UseHttpsRedirection();
```

### Step B4 — Global Exception Handler

> **SAY:** "We have a global exception handler middleware. If any unhandled exception occurs, it returns a generic error message — never exposing stack traces or internal details to clients. This prevents information leakage."

Show `ExceptionHandlerMiddleware.cs`:
```csharp
result.errorMsg = "An unexpected error occurred. Please try again later.";
```

### Step B5 — Input Validation (FluentValidation Pipeline)

> **SAY:** "All incoming requests pass through a FluentValidation pipeline behavior before reaching any handler. This is our first line of defense against injection — we validate format and constraints before any database interaction."

### Step B6 — Forwarded Headers

> **SAY:** "We use `UseForwardedHeaders` to correctly read the real client IP and protocol when behind a reverse proxy or load balancer. This ensures rate limiting and audit logging capture the correct client IP, not the proxy's IP."

### Step B7 — Hangfire Dashboard Security

> **SAY:** "The Hangfire dashboard is only available in Development environment and restricted to local requests only. In production, it's completely inaccessible."

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()]
});
```

### Step B8 — Sensitive Data Redaction

> **SAY:** "In our audit log middleware, we automatically redact sensitive fields — password, token, secret, apiKey, authorization — from request bodies and query strings. This ensures no sensitive data is stored in our logs."

### Summary of Security Implementations

> **SAY:** "In total, we have 8 layers of security:"
> 1. Server header suppression (anti-fingerprinting)
> 2. Security response headers (XSS, clickjacking, MIME-sniffing, CSP)
> 3. IP-based rate limiting (anti-abuse, anti-DDoS)
> 4. HTTPS enforcement (data in transit encryption)
> 5. Global exception handling (no information leakage)
> 6. Input validation pipeline (anti-injection)
> 7. Forwarded headers (correct IP behind proxy)
> 8. Sensitive data redaction in logs (data privacy)

---

## 5. Part C — Audit Log Explanation

> **SAY:** "Now let me show the Audit Log. Every API request that hits our endpoints is recorded."

### Step C1 — Show Audit Log Table in SSMS

```sql
SELECT TOP 20 * FROM [AuditLog] ORDER BY [Id] DESC;
```

> **SAY:** "You can see the requests we just made in Postman are all logged here."

### Step C2 — Walk Through Each Column

Go through each column explaining its purpose:

| Column | Type | Description | Script to Say |
|--------|------|-------------|---------------|
| **Id** | `int` (PK, auto-increment) | Unique row identifier | "Auto-generated primary key. We need this for database indexing and to uniquely reference any audit record." |
| **TraceId** | `nvarchar(100)` | Distributed trace correlation ID | "This links the audit log to application logs and any upstream/downstream service calls. If a user reports an issue, we give them the traceId and can reconstruct the exact request path across all systems." |
| **HttpMethod** | `nvarchar(10)` | GET, POST, PUT, DELETE, etc. | "Records which HTTP verb was used. Essential for understanding what kind of operation was performed — read vs write." |
| **Endpoint** | `nvarchar(500)` | The URL path (e.g., `/api/v1/exchange-rates`) | "The exact endpoint that was called. We need this to analyze traffic patterns — which endpoints are most used, and to investigate specific requests." |
| **QueryString** | `nvarchar(2000)` | Query parameters (e.g., `?currency=USD`) | "Stores the query parameters. This is critical for debugging — we can see exactly what filters or parameters the caller provided. Sensitive values are redacted." |
| **RequestHeaders** | `nvarchar(max)` | Selected headers as JSON | "We capture Content-Type, Accept, User-Agent, and X-Forwarded-For headers. These help identify the caller type (browser, Postman, another service) and the request format." |
| **RequestBody** | `nvarchar(max)` | Request body (for POST/PUT) | "For write operations, we store the request payload. This is essential for auditing what data was submitted. Sensitive fields are automatically redacted. Body is truncated at 4KB to prevent storage bloat." |
| **ResponseStatusCode** | `int` | HTTP status code (200, 400, 404, 500) | "The outcome of the request. We use this to monitor error rates and identify problematic endpoints. We index this column for fast filtering." |
| **ResponseBody** | `nvarchar(max)` | Response body (only for non-200) | "We only capture the response body for error responses (non-200). Success responses can be large and are not useful for debugging. Error responses contain error codes and messages we need for investigation." |
| **ClientIpAddress** | `nvarchar(50)` | Caller's IP address | "The real IP of the caller. We resolve this from X-Forwarded-For first (for proxy scenarios), then fall back to the direct connection IP. This is critical for security auditing — identifying who made each request." |
| **DurationMs** | `bigint` | Request duration in milliseconds | "How long the entire request took. This is our performance metric. If a request took 5000ms, we know there's a performance issue. We can identify slow endpoints and optimize." |
| **CreatedOn** | `datetime` | Timestamp of the request | "When the request was made. We index this column for time-based queries. Useful for incident investigation — 'what happened at 2:15 PM?'" |

### Step C3 — Show Indexed Columns

> **SAY:** "We have database indexes on three columns for fast querying:"
> - `TraceId` — for looking up a specific request chain
> - `CreatedOn` — for time-range analysis
> - `ResponseStatusCode` — for filtering errors

### Step C4 — Demonstrate a Query

```sql
-- Find all failed requests in the last hour
SELECT * FROM [AuditLog] 
WHERE ResponseStatusCode >= 400 
AND CreatedOn >= DATEADD(HOUR, -1, GETDATE())
ORDER BY CreatedOn DESC;
```

> **SAY:** "This is a typical query we'd use to investigate issues. We can quickly filter by status code and time range."

---

## 6. Part D — Application Log Explanation

> **SAY:** "Now let's look at Application Logging — this is different from Audit Log. Audit Log captures HTTP request/response metadata. Application Log captures internal system events, errors, and business logic flow."

### Step D1 — Show Application Log Table in SSMS

```sql
SELECT TOP 20 * FROM [ApplicationLog] ORDER BY [Id] DESC;
```

### Step D2 — Walk Through Each Column

| Column | Type | Description | Script to Say |
|--------|------|-------------|---------------|
| **Id** | `int` (PK, auto-increment) | Unique log entry identifier | "Auto-generated primary key for each log entry." |
| **Message** | `nvarchar(max)` | The rendered log message | "The actual log message with parameters substituted in. For example: 'GetRate request received: currency=USD, date=2025-03-11'. This is the human-readable output." |
| **Level** | `nvarchar(128)` | Log severity (Information, Warning, Error, Fatal) | "The severity level. We use this to filter by importance. I'll explain our minimum level strategy next." |
| **TimeStamp** | `datetime` | When the log was written | "Exact timestamp of the log entry. Essential for correlating events in chronological order." |
| **Exception** | `nvarchar(max)` | Full exception details (if applicable) | "If the log entry is about an exception, the full stack trace and exception message is stored here. Critical for debugging production errors." |
| **LogEvent** | `nvarchar(max)` | Structured log data in JSON format | "The full structured log event as JSON. This contains all log properties in machine-readable format — useful for log aggregation tools and automated analysis." |
| **SourceContext** | `nvarchar(500)` | Fully qualified class name that wrote the log | "Tells us exactly which class generated this log. For example, `Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates.ExchangeRateQueryHandler`. This is critical for filtering — we can see all logs from a specific service layer." |
| **TraceId** | `nvarchar(100)` | Distributed trace correlation ID | "Same trace ID as in the Audit Log. This is how we link application logs to their corresponding audit log entry. One HTTP request = one TraceId across both tables." |

### Step D3 — Minimum Log Levels Explained

> **SAY:** "We have a very deliberate strategy for log levels in different environments and sinks."

#### Database (SQL Server) — Minimum: `Information`

```json
"WriteTo": [
    {
        "Name": "MSSqlServer",
        "Args": {
            "restrictedToMinimumLevel": "Information"
        }
    }
]
```

> **SAY:** "We store **Information** level and above (Information, Warning, Error, Fatal) in the database. Here's why:"
> - **Information** logs capture business-significant events — 'Sync succeeded', 'Saved 20 exchange rates', 'Hangfire job started'. These are needed for operational monitoring and compliance.
> - **Debug** logs are excluded because they are high-volume and would bloat the database. Debug logs include things like 'Repository query called', 'mapping started' — internal tracing that's only useful during development.
> - This strikes the balance between **having enough data for production debugging** and **not overwhelming storage**.

#### Console — Production: `Warning` / Development: `Debug`

**Production (`appsettings.json`):**
```json
{ "Name": "Console", "Args": { "restrictedToMinimumLevel": "Warning" } }
```

> **SAY:** "In production, we only show Warning and above on the console. Why?"
> - Console output in production goes to container logs (Docker/Kubernetes).
> - We don't want to flood container logs with informational messages — they cost storage and make it harder to spot real issues.
> - Warnings and Errors are actionable — they need attention.

**Development (`appsettings.Development.json`):**
```json
{ "Name": "Console", "Args": { "restrictedToMinimumLevel": "Debug" } }
```

> **SAY:** "In development, we set console to Debug — developers see everything including repository calls, handler logic flow, and detailed tracing. This helps during local debugging without needing a database query."

#### Microsoft/System Framework Overrides: `Error`

```json
"Override": {
    "Microsoft": "Error",
    "System": "Error"
}
```

> **SAY:** "We suppress Microsoft and System framework logs to Error-only. By default, ASP.NET Core and Entity Framework generate massive amounts of Information/Warning logs about request routing, middleware execution, and SQL queries. These would drown our business logs. We only want to see framework logs if something actually breaks."

### Step D4 — Show Serilog Enrichers

> **SAY:** "We enrich every log entry with three additional properties:"

| Enricher | Purpose |
|----------|---------|
| `FromLogContext` | Adds any properties pushed to the `LogContext` (scoped per request) |
| `WithMethodName` | Custom enricher — adds the method name that generated the log (e.g., `.Handle`, `.SyncSessionAsync`) |
| `WithSpan` | Adds the distributed tracing `TraceId` and `SpanId` from `System.Diagnostics.Activity` |

> **SAY:** "The `WithMethodName` is a custom enricher we built. It walks the call stack to find the method name from the `SourceContext` class. This means in the logs, you see not just 'ExchangeRateQueryHandler' but 'ExchangeRateQueryHandler.Handle' — pinpointing the exact method."

### Step D5 — Demonstrate Cross-Referencing

```sql
-- Pick a traceId from AuditLog
DECLARE @traceId NVARCHAR(100) = '<paste-traceId-from-AuditLog>';

-- Find the audit log entry
SELECT * FROM [AuditLog] WHERE TraceId = @traceId;

-- Find all application logs for the same request
SELECT * FROM [ApplicationLog] WHERE TraceId = @traceId ORDER BY [TimeStamp];
```

> **SAY:** "This is the power of having TraceId in both tables. One Postman request generates an audit log row AND multiple application log rows. We can trace the full lifecycle: request received → validation → repository call → response sent."

---

## 7. Part E — Program.cs Walkthrough

> **SAY:** "Let me walk through Program.cs. This is the application bootstrap — every feature is registered here."

Open `Program.cs` in the editor.

### Section 1: Bootstrap (Line 17)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
```

> **SAY:** "We use the minimal hosting model. First thing we do is suppress the Kestrel server header — this is a security hardening step to prevent server fingerprinting."

### Section 2: Serilog Configuration (Line 25)

```csharp
ConfigureLog(builder.Host);
```

> **SAY:** "Serilog is configured early, before anything else, so every subsequent log during startup is captured. The configuration reads from appsettings.json — database sink, console sink, enrichers, minimum levels."

### Section 3: Multi-Layer Service Registration (Lines 31–33)

```csharp
builder.Services.RegisterServiceModule(builder.Configuration);          // Service layer
builder.Services.RegisterInfrastructureModule(builder.Configuration);   // Infrastructure layer
builder.Services.RegisterSharedServiceModule(builder.Configuration);    // Shared layer
```

> **SAY:** "We follow Clean Architecture with separate registration per layer:"
> - **Service Module**: Mediator, FluentValidation validators, BNM API options
> - **Infrastructure Module**: Entity Framework DbContext, repositories, Unit of Work, interceptors
> - **Shared Module**: Hangfire, HttpClient with Polly retry/timeout policies

### Section 4: Mediator & AutoMapper (Lines 36–37)

```csharp
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

> **SAY:** "We use the Mediator pattern (source-generated) for CQRS — Commands and Queries. AutoMapper handles ViewModel-to-Query/Command mapping. Scoped lifetime ensures each request gets its own mediator scope."

### Section 5: API Versioning (Line 43)

> **SAY:** "We implement URL-segment versioning: `/api/v1/exchange-rates`. This ensures backward compatibility — v2 can be introduced without breaking existing consumers."

### Section 6: Middleware Pipeline (Lines 62–102)

> **SAY:** "The middleware order matters. Here's our pipeline:"

```
ForwardedHeaders → Security Headers → ExceptionHandler → Rate Limiting → 
Swagger (dev only) → HTTPS Redirection → AuditLog → Auth → Controllers
```

> **SAY:** "Key ordering decisions:"
> - **ExceptionHandler** is early — catches exceptions from all downstream middleware
> - **Rate Limiting** is before any business logic — blocks abuse before processing starts
> - **AuditLog** is after HTTPS redirection — ensures we only log real API traffic
> - **AuditLog** is before Auth — captures even unauthenticated requests for security monitoring

### Section 7: Hangfire Job Registration (Lines 108–145)

> **SAY:** "Hangfire recurring jobs are configured from appsettings — 4 daily jobs for each BNM session (0900, 1130, 1200, 1700). Timezone-aware, configurable cron expressions, and each job has a configurable date offset. If the timezone config is invalid, we gracefully fall back to local time."

---

## 8. Part F — Audit Log Middleware Code Walkthrough

> **SAY:** "Let me walk through the AuditLogMiddleware in detail. This is the most critical piece for compliance and debugging."

Open `AuditLogMiddleware.cs` in the editor.

### F1 — Skip Paths

```csharp
private static readonly string[] SkipPaths = ["/swagger", "/hangfire", "/favicon.ico", "/health"];
```

> **SAY:** "We skip logging for development/infrastructure endpoints. Swagger and Hangfire dashboard would generate noise. Health checks are high-frequency and not useful for auditing."

### F2 — Selective Header Capture

```csharp
private static readonly string[] CapturedHeaders = ["Content-Type", "Accept", "User-Agent", "X-Forwarded-For"];
```

> **SAY:** "We don't capture ALL headers — that would include sensitive ones like Authorization. We only capture the four headers that help identify the caller and request format."

### F3 — Sensitive Field Redaction

```csharp
private static readonly string[] SensitiveFields = ["password", "token", "secret", "apiKey", "authorization"];
```

> **SAY:** "Any field in request body or query string matching these names gets replaced with `***REDACTED***`. This is a data privacy requirement."

### F4 — Body Size Limit

```csharp
private const int MaxBodyLength = 4096; // 4KB max body capture
```

> **SAY:** "We truncate bodies at 4KB to prevent storage bloat. If someone sends a 10MB payload, we only store the first 4KB plus `...[truncated]` marker."

### F5 — Request Flow

```csharp
context.Request.EnableBuffering();
```

> **SAY:** "EnableBuffering allows us to read the request body without consuming it. The body stream can be read by both our middleware and the controller."

### F6 — Client IP Resolution

```csharp
var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";
```

> **SAY:** "We check X-Forwarded-For first (set by reverse proxies/load balancers), take only the first IP (the original client), then fall back to the direct connection IP."

### F7 — Response Body Capture Strategy

```csharp
if (context.Response.StatusCode != 200)
{
    responseBody = await CaptureResponseBodyAsync(responseBodyStream);
}
```

> **SAY:** "We only capture response bodies for non-200 status codes. Why? Success responses can be large (hundreds of exchange rates) and have no debugging value. Error responses are small and essential for troubleshooting."

### F8 — Fire-and-Forget Database Save

```csharp
_ = Task.Run(async () =>
{
    using var scope = _scopeFactory.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.AuditLogs.Add(auditLog);
    await dbContext.SaveChangesAsync();
});
```

> **SAY:** "We save the audit log on a background thread using `Task.Run`. This is intentional — audit logging should never slow down the actual API response. The caller gets their response immediately, and the audit log is written asynchronously."
>
> "We use `IServiceScopeFactory` to create a new DI scope because the original request scope will be disposed before the background task completes."

### F9 — Fault Tolerance

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "AuditLogMiddleware: Failed to save audit log...");
}
```

> **SAY:** "If audit logging fails (database down, disk full), we catch the exception and log it — but the API continues to serve requests normally. Audit logging should never crash the application."

---

## 9. Closing Summary

> **SAY:** "To summarize what we've built:"
>
> 1. **Clean Architecture** — 6 projects with clear separation of concerns
> 2. **CQRS + Mediator** — Commands and Queries separated with pipeline behaviors
> 3. **8 security layers** — From header hardening to input validation to rate limiting
> 4. **Dual logging system** — Audit Log (HTTP metadata) + Application Log (business events)
> 5. **Distributed tracing** — TraceId links everything across both log tables
> 6. **Environment-appropriate logging** — Debug in dev console, Warning in prod console, Information in database
> 7. **Resilient integrations** — Polly retry + timeout for BNM API calls
> 8. **Automated sync** — Hangfire cron jobs for all 4 BNM sessions
> 9. **Non-blocking audit** — Fire-and-forget pattern keeps API fast

> **SAY:** "Any questions?"

---

## Quick Reference — Postman Endpoints

| # | Method | Endpoint | Purpose |
|---|--------|----------|---------|
| 1 | GET | `/api/v1/exchange-rates` | Get all rates (latest date) |
| 2 | GET | `/api/v1/exchange-rates?currency=USD` | Get rate for specific currency |
| 3 | GET | `/api/v1/exchange-rates?date=2025-03-11` | Get all rates for specific date |
| 4 | GET | `/api/v1/exchange-rates?currency=USD&date=2025-03-11` | Get specific rate + date |
| 5 | GET | `/api/v1/exchange-rates?date=invalid` | Trigger validation error (400) |
| 6 | GET | `/api/v1/exchange-rates?currency=XYZ` | Trigger not found (404) |
| 7 | GET | `/api/v1/exchange-rates/currencies` | Get all currencies |

## Quick Reference — SSMS Queries

```sql
-- View latest audit logs
SELECT TOP 20 * FROM [AuditLog] ORDER BY [Id] DESC;

-- View latest application logs
SELECT TOP 20 * FROM [ApplicationLog] ORDER BY [Id] DESC;

-- Cross-reference by TraceId
DECLARE @traceId NVARCHAR(100) = '<your-traceId>';
SELECT * FROM [AuditLog] WHERE TraceId = @traceId;
SELECT * FROM [ApplicationLog] WHERE TraceId = @traceId ORDER BY [TimeStamp];

-- Find all errors in last hour
SELECT * FROM [AuditLog] WHERE ResponseStatusCode >= 400 AND CreatedOn >= DATEADD(HOUR, -1, GETDATE());
SELECT * FROM [ApplicationLog] WHERE Level IN ('Error', 'Fatal') AND TimeStamp >= DATEADD(HOUR, -1, GETDATE());
```
