# Unity Exchange Rates API — Technical Implementation Details

> **Purpose:** Reference document covering all implementation details — security, audit log, application log, Program.cs, and middleware.  
> **No demo script** — this is a standalone explanation document.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Security Implementations](#2-security-implementations)
3. [Audit Log — Complete Details](#3-audit-log--complete-details)
4. [Application Log — Complete Details](#4-application-log--complete-details)
5. [Log Level Strategy](#5-log-level-strategy)
6. [Program.cs — Implementation Breakdown](#6-programcs--implementation-breakdown)
7. [Audit Log Middleware — Code Explanation](#7-audit-log-middleware--code-explanation)

---

## 1. Architecture Overview

```
Unity.ExchangeRates.svc (Solution)
├── Unity.ExchangeRates.Api            → Entry point, controllers, middlewares, configurations
├── Unity.ExchangeRates.Service        → Mediator handlers, validators, business logic
├── Unity.ExchangeRates.Domain         → Entity models (Currency, ExchangeRateHistory, AuditLog)
├── Unity.ExchangeRates.Infrastructure → EF Core DbContext, repositories, interceptors
├── Unity.ExchangeRates.Repository     → Repository interfaces (contracts)
└── Unity.ExchangeRates.Shared         → Hangfire jobs, HttpClient with Polly policies
```

**Pattern:** Clean Architecture + CQRS (Mediator) + Repository + Unit of Work

---

## 2. Security Implementations

### 2.1 Full Security Summary Table

| # | Security Measure | Location | OWASP Category | Details |
|---|-----------------|----------|----------------|---------|
| 1 | Server Header Suppression | `Program.cs` line 20 | Security Misconfiguration (A05:2021) | Removes `Server: Kestrel` header to prevent server fingerprinting. Attackers use server identification to find known vulnerabilities specific to that server technology. |
| 2 | X-Content-Type-Options: nosniff | Inline middleware in `Program.cs` | Security Misconfiguration (A05:2021) | Prevents browsers from MIME-sniffing the response content type. Without this, a browser might interpret a JSON response as HTML and execute embedded scripts, enabling XSS attacks. |
| 3 | X-Frame-Options: DENY | Inline middleware in `Program.cs` | Security Misconfiguration (A05:2021) | Prevents the response from being rendered inside an `<iframe>`. This mitigates clickjacking attacks where an attacker overlays a transparent iframe over a legitimate page to trick users into clicking hidden elements. |
| 4 | Referrer-Policy: strict-origin-when-cross-origin | Inline middleware in `Program.cs` | Security Misconfiguration (A05:2021) | Controls how much referrer URL information is sent when navigating from this API. Prevents leaking internal API paths (which may contain query parameters with sensitive data) to external domains. |
| 5 | Content-Security-Policy: default-src 'none' | Inline middleware in `Program.cs` | Security Misconfiguration (A05:2021) | Instructs browsers to not load any resources (scripts, stylesheets, images, fonts, etc.) from any source. Since this is a pure JSON API, no resources should ever be loaded. This is the strictest CSP possible. |
| 6 | IP-Based Rate Limiting | `AspNetCoreRateLimit` library, configured in `appsettings.json` | Security Misconfiguration (A05:2021) | Prevents abuse, brute-force attacks, and DDoS. Limits general endpoints to 60 requests/min/IP. Sensitive write endpoints (sync) limited to 5/min/IP. Returns HTTP 429 when exceeded. |
| 7 | HTTPS Redirection | `app.UseHttpsRedirection()` in `Program.cs` | Cryptographic Failures (A02:2021) | Ensures all communication is encrypted in transit. HTTP requests are automatically redirected to HTTPS. Prevents man-in-the-middle attacks and eavesdropping. |
| 8 | Global Exception Handler | `ExceptionHandlerMiddleware.cs` | Security Misconfiguration (A05:2021) | Catches all unhandled exceptions and returns a generic error message. Never exposes stack traces, internal error details, database information, or file paths to the client. Prevents information leakage. |
| 9 | Input Validation Pipeline | `RequestValidationBehavior.cs` + FluentValidation | Injection (A03:2021) | All incoming requests are validated before reaching any handler. Date format validation, string length constraints, and required field checks prevent malformed data from reaching the database layer. |
| 10 | Forwarded Headers | `app.UseForwardedHeaders()` in `Program.cs` | Broken Access Control (A01:2021) | Correctly resolves `X-Forwarded-For` and `X-Forwarded-Proto` from reverse proxies. Ensures rate limiting and audit logging capture the real client IP, not the proxy IP. |
| 11 | Hangfire Dashboard Restriction | `Program.cs` (Development only) | Broken Access Control (A01:2021) | Dashboard is only available in Development environment, restricted to localhost connections only (`LocalRequestsOnlyAuthorizationFilter`). Never exposed in production. |
| 12 | Sensitive Data Redaction | `AuditLogMiddleware.cs` | Sensitive Data Exposure (A02:2021) | Automatically redacts fields named password, token, secret, apiKey, authorization from request bodies and query strings before writing to audit log. Ensures no credentials are stored in logs. |
| 13 | Selective Header Logging | `AuditLogMiddleware.cs` | Sensitive Data Exposure (A02:2021) | Only captures 4 safe headers (Content-Type, Accept, User-Agent, X-Forwarded-For). Excludes Authorization and Cookie headers to prevent credential leakage in logs. |
| 14 | Body Size Truncation | `AuditLogMiddleware.cs` | Denial of Service | Request/response bodies are capped at 4KB in audit logs. Prevents storage exhaustion from large payloads being logged. |

### 2.2 Security Headers — How They Work Together

```
Client Request
    │
    ▼
┌─────────────────────────┐
│  Server Header Removed  │  ← Attacker cannot identify Kestrel
├─────────────────────────┤
│  X-Content-Type-Options │  ← Browser won't MIME-sniff
│  X-Frame-Options        │  ← Cannot be loaded in iframe
│  Referrer-Policy        │  ← Internal URLs stay private
│  Content-Security-Policy│  ← No scripts/styles can load
├─────────────────────────┤
│  Rate Limit Check       │  ← Block if limit exceeded (429)
├─────────────────────────┤
│  HTTPS Redirection      │  ← Force encryption
├─────────────────────────┤
│  Input Validation       │  ← Reject malformed input (400)
├─────────────────────────┤
│  Exception Handler      │  ← Mask internal errors (500)
└─────────────────────────┘
```

### 2.3 Rate Limit Configuration Details

| Rule | Endpoint Pattern | Period | Limit | Reason |
|------|-----------------|--------|-------|--------|
| General | `*` (all endpoints) | 1 minute | 60 (prod) / 120 (dev) | Prevents general API abuse. 60 req/min is generous for legitimate use but blocks automated scraping or brute-force. |
| Sync Endpoint | `post:/api/*/exchange-rates/sync` | 1 minute | 5 (prod) / 10 (dev) | Sync triggers writes to the database + external BNM API calls. Must be heavily restricted to prevent data corruption and upstream throttling. |

**Behavior:**
- `StackBlockedRequests: false` — Blocked requests do NOT count against the limit. Only successful requests consume the quota.
- `HttpStatusCode: 429` — Standard "Too Many Requests" response.
- `EnableEndpointRateLimiting: true` — Each endpoint has its own counter. Calling `/currencies` doesn't consume the `/exchange-rates` quota.

---

## 3. Audit Log — Complete Details

### 3.1 Purpose

The Audit Log records **every HTTP request** that reaches the API (excluding infrastructure endpoints). It answers:
- **Who** called the API? (ClientIpAddress)
- **What** did they request? (HttpMethod, Endpoint, QueryString, RequestBody)
- **When** did they call? (CreatedOn)
- **What was the result?** (ResponseStatusCode, ResponseBody, DurationMs)
- **How do we trace it?** (TraceId)

### 3.2 Database Table Schema

| # | Column Name | Data Type | Nullable | Constraints | Description | Why We Store It |
|---|------------|-----------|----------|-------------|-------------|----------------|
| 1 | **Id** | `int` | No | PK, Identity (auto-increment) | Unique row identifier for each audit record | Required as primary key for database operations. Auto-incremented to guarantee uniqueness. Provides a monotonic ordering for sequential analysis. |
| 2 | **TraceId** | `nvarchar(100)` | Yes | Indexed | Distributed trace correlation ID from `System.Diagnostics.Activity` or `HttpContext.TraceIdentifier` | **Critical for cross-table correlation.** Links this audit entry to all Application Log entries for the same request. When a user reports an issue, the TraceId from the API response enables full request reconstruction across all logs and services. Indexed for fast lookups. |
| 3 | **HttpMethod** | `nvarchar(10)` | No | Required | HTTP verb: GET, POST, PUT, DELETE, PATCH, OPTIONS | Identifies the **type of operation**. GET = read, POST = write. Essential for distinguishing read vs write operations in security auditing. Helps identify unexpected write operations that could indicate an attack. |
| 4 | **Endpoint** | `nvarchar(500)` | No | Required | URL path (e.g., `/api/v1/exchange-rates`) | Identifies **which resource** was accessed. Critical for traffic pattern analysis (which endpoints are hot), access pattern monitoring, and identifying unauthorized endpoint access attempts. |
| 5 | **QueryString** | `nvarchar(2000)` | Yes | — | URL query parameters (e.g., `?currency=USD&date=2025-03-11`) | Records the **exact filter parameters** of the request. Essential for debugging — "why did this request return 404?" can be answered by checking if the currency/date was valid. Sensitive values are automatically redacted (e.g., `?apiKey=***REDACTED***`). |
| 6 | **RequestHeaders** | `nvarchar(max)` | Yes | — | Selected headers stored as JSON: `{"Content-Type":"application/json","User-Agent":"PostmanRuntime/7.37"}` | Captures **caller identity and preferences**. Content-Type tells us the request format. User-Agent identifies the client (Postman, browser, another service, curl). X-Forwarded-For reveals the proxy chain. Accept shows expected response format. Only 4 safe headers are captured — Authorization and Cookie headers are excluded for security. |
| 7 | **RequestBody** | `nvarchar(max)` | Yes | Max 4KB | Request payload (for POST/PUT requests). Sensitive fields redacted. | Records **what data was submitted** in write operations. Essential for auditing data changes — "what values did the sync command send?" Truncated at 4KB to prevent storage bloat from large payloads. Automatically redacts fields named password, token, secret, apiKey, authorization. |
| 8 | **ResponseStatusCode** | `int` | No | Indexed | HTTP status code (200, 400, 404, 429, 500) | Records the **outcome** of each request. Indexed for fast error rate analysis. Enables queries like "how many 500 errors occurred today?" and "which endpoints have the highest error rate?" Used for SLA monitoring and alerting. |
| 9 | **ResponseBody** | `nvarchar(max)` | Yes | Only captured for non-200 responses | Response payload (error details) | **Only stored for error responses** (non-200 status codes). Success responses can be large (list of exchange rates) and have no debugging value. Error responses contain the error code and message — essential for understanding why a request failed. Saves significant storage by skipping 200 responses. |
| 10 | **ClientIpAddress** | `nvarchar(50)` | Yes | — | Real client IP address, resolved from X-Forwarded-For or direct connection | Identifies **who made the request**. Critical for security auditing — detecting suspicious IP addresses, geographic anomalies, brute-force attempts from a single IP. Resolved from X-Forwarded-For header first (for proxy/LB scenarios), falls back to direct connection IP. |
| 11 | **DurationMs** | `bigint` | No | — | Request processing time in milliseconds | Records **how long each request took**. This is our primary performance metric. Enables SLA monitoring ("are we meeting our 500ms target?"), identifies slow endpoints, and detects performance degradation over time. Can correlate slow requests with specific parameters or time periods. |
| 12 | **CreatedOn** | `datetime` | No | Indexed, Default: `GETDATE()` | Timestamp when the audit record was created | Records **when the request occurred**. Indexed for fast time-range queries ("show me all requests from 2 PM to 3 PM"). Essential for incident investigation — correlating issues with specific timeframes. Uses database server time for consistency. |

### 3.3 Database Indexes

| Index | Column(s) | Purpose |
|-------|-----------|---------|
| PK (Clustered) | `Id` | Primary key, sequential ordering |
| Non-Clustered | `TraceId` | Fast lookup by trace ID — "find the audit log for this specific request" |
| Non-Clustered | `CreatedOn` | Fast time-range queries — "show all requests in the last hour" |
| Non-Clustered | `ResponseStatusCode` | Fast error filtering — "show all 500 errors today" |

### 3.4 What Is Skipped (Not Logged)

| Path | Reason |
|------|--------|
| `/swagger/*` | Swagger UI generates many resource requests (HTML, CSS, JS). These are development tooling, not business requests. |
| `/hangfire/*` | Hangfire dashboard generates internal polling requests every few seconds. Would create noise in audit log. |
| `/favicon.ico` | Browser automatically requests this. Not an API call. |
| `/health` | Health check endpoints are called frequently by load balancers/orchestrators. Would flood the audit log. |

### 3.5 Data Sanitization Rules

| Rule | Target | Behavior | Example |
|------|--------|----------|---------|
| Sensitive field redaction | Request body JSON properties | Fields with names containing "password", "token", "secret", "apiKey", "authorization" are replaced with `***REDACTED***` | `{"apiKey": "abc123"}` → `{"apiKey": "***REDACTED***"}` |
| Query string redaction | URL query parameters | Same sensitive field names redacted in query strings | `?token=xyz` → `?token=***REDACTED***` |
| Body truncation | Request/Response body | Truncated at 4,096 characters (4KB) with `...[truncated]` marker | Large payloads are capped to prevent storage bloat |
| Header filtering | Request headers | Only 4 headers captured: Content-Type, Accept, User-Agent, X-Forwarded-For | Authorization, Cookie, and all other headers are excluded |

---

## 4. Application Log — Complete Details

### 4.1 Purpose

The Application Log records **internal system events** — when code executes, what decisions it makes, and any errors that occur. While the Audit Log captures the "outside view" (HTTP request/response), the Application Log captures the "inside view" (what happened within the code).

### 4.2 Database Table Schema (Serilog MSSqlServer Sink — `ApplicationLog`)

| # | Column Name | Data Type | Nullable | Description | Why We Store It |
|---|------------|-----------|----------|-------------|----------------|
| 1 | **Id** | `int` | No | PK, Identity (auto-increment) | Standard primary key for database operations and sequential ordering. |
| 2 | **Message** | `nvarchar(max)` | Yes | The fully rendered log message with parameters substituted | The **human-readable log message**. Example: `"ExchangeRateQueryHandler: Success for currency=USD, date=2025-03-11, session=1700"`. This is the most frequently read column — operators and developers scan these messages to understand system behavior. Parameters are substituted in (structured logging), making each message immediately understandable without needing to reference external data. |
| 3 | **Level** | `nvarchar(128)` | Yes | Serilog log level: Debug, Information, Warning, Error, Fatal | The **severity classification**. Determines how urgently this log needs attention. Used for filtering — "show me only errors" or "show me everything including debug". The level drives alerting rules (Error → notify on-call engineer, Fatal → page the team). |
| 4 | **TimeStamp** | `datetime` | Yes | When the log event was created | **Precise timing of internal events**. Unlike Audit Log's CreatedOn (which records when the HTTP request arrived), this records when each code line executed. Essential for understanding execution order — does the repository call happen before or after the validation? How long between handler start and response? |
| 5 | **Exception** | `nvarchar(max)` | Yes | Full exception details: type, message, and stack trace | The **complete error details** including stack trace. This is the most valuable column for debugging production errors. Contains the exception type (e.g., `SqlException`), message (e.g., `Connection timeout`), and full call stack showing exactly where the error originated. Only populated when the log entry includes an exception. |
| 6 | **LogEvent** | `nvarchar(max)` | Yes | Full structured log event serialized as JSON | The **machine-readable version** of the log. Contains all properties in structured format — useful for log aggregation tools (Elasticsearch, Splunk, Azure Monitor) that can parse and index JSON properties. Includes enriched properties like MethodName, SpanId, and any custom properties pushed via LogContext. This was added (via `addStandardColumns: ["LogEvent"]`) for future integration with log analysis platforms. |
| 7 | **SourceContext** | `nvarchar(500)` | Yes | Fully qualified class name (custom column) | The **class that wrote this log**. Example: `"Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates.ExchangeRateQueryHandler"`. Critical for filtering — "show me all logs from the Repository layer" or "show me only Hangfire job logs". Helps developers immediately identify which layer and class generated the event. Custom column added via Serilog config. |
| 8 | **TraceId** | `nvarchar(100)` | Yes | Distributed trace correlation ID (custom column) | The **cross-table correlation key**. Same value as the Audit Log's TraceId column. Enables joining Application Log entries with their corresponding Audit Log record. One HTTP request generates one Audit Log row and multiple Application Log rows — TraceId links them all. Custom column enriched by `Serilog.Enrichers.Span`. |

### 4.3 Removed Standard Columns

| Removed Column | Reason |
|----------------|--------|
| `MessageTemplate` | The raw message template (e.g., `"Fetching rate for {Currency}"`) — redundant when we have both `Message` (rendered) and `LogEvent` (structured). Saves storage. |
| `Properties` | XML-serialized properties — replaced by `LogEvent` (JSON) which is more readable and better for log aggregation. Saves storage and avoids XML parsing overhead. |

### 4.4 Serilog Enrichers Explained

| Enricher | Source | What It Adds | Why |
|----------|--------|-------------|-----|
| `FromLogContext` | `Serilog.Enrichers.LogContext` | Any property pushed to `LogContext.PushProperty()` within a scope | Allows scoped properties — any code in the request pipeline can push additional properties (e.g., `UserId`, `CorrelationId`) that automatically appear in all log entries within that scope. Enables request-scoped enrichment without passing values through every method. |
| `WithMethodName` | Custom: `LogMethodNameEnricher.cs` | `MethodName` property (e.g., `.Handle`, `.SyncSessionAsync`) | Pinpoints the **exact method** — not just the class but the specific method. When `SourceContext` says `ExchangeRateQueryHandler`, MethodName adds `.Handle`. This eliminates ambiguity in classes with multiple methods. The enricher walks the stack trace to find the calling method from the SourceContext class. |
| `WithSpan` | `Serilog.Enrichers.Span` NuGet package | `TraceId` and `SpanId` from `System.Diagnostics.Activity` | Injects **distributed tracing IDs** into every log entry. TraceId is the same across all services in a distributed call chain. SpanId identifies the specific operation within the trace. This is what enables cross-table correlation with the Audit Log and cross-service tracing in distributed systems. |

### 4.5 What Gets Logged at Each Layer

| Layer | Class | Log Level | Example Message |
|-------|-------|-----------|-----------------|
| **Controller** | `ExchangeRateController` | Debug | `"GetRate request received: currency=USD, date=2025-03-11"` |
| **Validation** | `RequestValidationBehavior` | Warning | `"Validation Error: [{errorCode: 00400, errorMsg: 'Date must be in yyyy-MM-dd format.'}]"` |
| **Query Handler** | `ExchangeRateQueryHandler` | Debug, Error | `"Fetching ALL rates for date=2025-03-11"`, `"No rate found in DB..."` |
| **Repository** | `ExchangeRateRepository` | Debug | `"GetActiveCurrenciesAsync returned 20 currencies"` |
| **Unit of Work** | `UnitOfWork` | Debug, Information, Warning | `"SaveChangesAsync persisted 20 changes"`, `"Transaction rolled back"` |
| **Hangfire Job** | `ExchangeRateSyncJob` | Information, Error, Critical | `"Sync succeeded for 2025-03-11 session=0900"`, `"Job crashed unexpectedly"` |
| **Exception Handler** | `ExceptionHandlerMiddleware` | Warning, Error | `"Validation exception: ..."`, `"Unhandled exception: ..."` |
| **Audit Middleware** | `AuditLogMiddleware` | Debug, Error | `"Saved audit log for GET /api/v1/exchange-rates → 200 (45ms)"` |

---

## 5. Log Level Strategy

### 5.1 Minimum Level by Sink and Environment

| Sink | Production (`appsettings.json`) | Development (`appsettings.Development.json`) | Reason |
|------|--------------------------------|----------------------------------------------|--------|
| **SQL Server (ApplicationLog table)** | `Information` | `Information` | Database stores business-significant events. Information level captures sync results, error details, and operational metrics. Debug would add 5-10x more rows with low value (repository trace logs). |
| **Console** | `Warning` | `Debug` | **Production:** Console goes to container logs (Docker/K8s). Only Warnings and Errors are actionable — everything else is noise that costs storage and makes it harder to spot real issues. **Development:** Developers need full visibility including Debug-level repository calls and handler logic flow for local debugging. |

### 5.2 Framework Override Levels

| Source | Override Level | Reason |
|--------|---------------|--------|
| `Microsoft.*` | `Error` | ASP.NET Core generates verbose Information/Warning logs about request routing, middleware execution, content negotiation, and HTTP/2 connection details. These would drown our business logs. We only want to see framework logs when something actually breaks (Error level). |
| `System.*` | `Error` | .NET System libraries generate logs about GC, thread pool, and socket operations. Only relevant when diagnosing system-level failures. |
| `Default` (our code) | `Information` (prod) / `Debug` (dev) | Our application code is what we care about. In production, Information captures business events. In development, Debug shows the full execution trace. |

### 5.3 Why Not Store Debug Logs in Database?

| Factor | Debug in DB | Information in DB (chosen) |
|--------|------------|--------------------------|
| Volume per request | ~8–12 rows | ~2–4 rows |
| Storage growth (1000 req/day) | ~8,000–12,000 rows/day | ~2,000–4,000 rows/day |
| Value for production debugging | High (but mostly noise) | Sufficient (captures key decisions) |
| Query performance | Slower (large table) | Faster (smaller table) |
| Cost | Higher storage + backup | Lower |

**Decision:** Information level in the database gives us sufficient production debugging capability while keeping the table manageable. Debug logs are available in real-time via console during development.

### 5.4 Log Level Reference

| Level | When To Use | Example | Stored In DB? | Shown In Console? |
|-------|------------|---------|---------------|-------------------|
| **Debug** | Detailed internal flow, method entry/exit, parameter values | `"Repository: GetActiveCurrenciesAsync called"` | No (dev & prod) | Dev only |
| **Information** | Business-significant events, successful operations | `"Sync succeeded for 2025-03-11 session=0900"` | Yes | Dev only |
| **Warning** | Unexpected but recoverable situations | `"Hangfire timezone not found, falling back to local"` | Yes | Yes (both) |
| **Error** | Failures that need investigation | `"Failed to save audit log for GET /api/v1/exchange-rates"` | Yes | Yes (both) |
| **Fatal** | Application is crashing or unusable | `"Application terminated unexpectedly"` | Yes | Yes (both) |

---

## 6. Program.cs — Implementation Breakdown

### 6.1 Complete Middleware Pipeline Order

```
Request →
  1. ForwardedHeaders          (resolve real IP/proto from proxy)
  2. Security Headers          (add X-Content-Type-Options, X-Frame-Options, etc.)
  3. ExceptionHandlerMiddleware (catch all unhandled exceptions → generic 500)
  4. IpRateLimiting            (block if over limit → 429)
  5. Swagger UI                (dev only — serve Swagger page)
  6. Hangfire Dashboard        (dev only — serve Hangfire page)
  7. HttpsRedirection          (redirect HTTP → HTTPS)
  8. AuditLogMiddleware        (record request/response to AuditLog table)
  9. Authentication            (validate identity — future)
  10. Authorization             (enforce permissions — future)
  11. Routing → Controller      (execute business logic)
← Response
```

### 6.2 Why This Order Matters

| Position | Middleware | Why Here |
|----------|-----------|----------|
| 1st | ForwardedHeaders | Must run before anything reads `RemoteIpAddress` or `Request.Scheme` — otherwise rate limiting and audit logging get the proxy IP. |
| 2nd | Security Headers | Applied to every response regardless of outcome (except dev UI paths). Must be before any short-circuit middleware. |
| 3rd | ExceptionHandler | Wraps everything below. If rate limiting or audit logging throws, this catches it. |
| 4th | Rate Limiting | Before any business logic. Blocked requests should not execute handlers or touch the database. |
| 7th | HTTPS Redirection | After dev-only middleware (Swagger/Hangfire) which serve their own content. |
| 8th | Audit Log | After HTTPS redirect — only logs real HTTPS API traffic. Before Auth — captures even unauthenticated requests for security monitoring. |
| 9th–10th | Auth | After Audit Log — we want to audit all requests, including those that fail authentication. |

### 6.3 Service Registration Modules

| Module | Method | What It Registers | Layer |
|--------|--------|------------------|-------|
| Service | `RegisterServiceModule()` | Mediator pipeline, FluentValidation validators, `BnmApiOptions`, `IHttpContextAccessor` | Business Logic |
| Infrastructure | `RegisterInfrastructureModule()` | `AppDbContext` (EF Core + SQL Server), `EntitySaveChangeInterceptor`, `ExchangeRateRepository`, `UnitOfWork` | Data Access |
| Shared | `RegisterSharedServiceModule()` | `BnmClient` HttpClient (with Polly retry/timeout), Hangfire server + storage, `ExchangeRateSyncJob` | Cross-Cutting |
| API (inline) | `AddMediator()`, `AddAutoMapper()` | Source-generated Mediator, AutoMapper profiles | Presentation |

### 6.4 API Versioning Configuration

```csharp
options.AssumeDefaultVersionWhenUnspecified = true;  // Unversioned requests → v1.0
options.DefaultApiVersion = new ApiVersion(1, 0);    // Default: v1.0
options.ReportApiVersions = true;                    // Response header: api-supported-versions
options.ApiVersionReader = new UrlSegmentApiVersionReader(); // Read from URL: /api/v1/...
```

| Setting | Value | Purpose |
|---------|-------|---------|
| `AssumeDefaultVersionWhenUnspecified` | `true` | Backward compatibility — old consumers without version in URL still work |
| `DefaultApiVersion` | `1.0` | The version assumed when not specified |
| `ReportApiVersions` | `true` | Adds `api-supported-versions` response header — consumers can discover available versions |
| `ApiVersionReader` | `UrlSegmentApiVersionReader` | Version is part of the URL path (e.g., `/api/v1/`), not a query string or header |

### 6.5 Hangfire Job Configuration

| Session | Job ID | Cron Expression | Schedule (MYT) | DateOffset | Purpose |
|---------|--------|-----------------|-----------------|------------|---------|
| 0900 | `exchange-rate-sync-0900` | `0 10 * * 1-5` | Mon–Fri 10:00 AM | 0 | Sync morning session — runs 1 hour after BNM publishes 0900 rates |
| 1130 | `exchange-rate-sync-1130` | `0 12 * * 1-5` | Mon–Fri 12:00 PM | 0 | Sync midday session |
| 1200 | `exchange-rate-sync-1200` | `0 13 * * 1-5` | Mon–Fri 1:00 PM | 0 | Sync noon session |
| 1700 | `exchange-rate-sync-1700` | `0 0 * * 2-6` | Tue–Sat 12:00 AM | -1 | Sync previous day's closing session — runs at midnight because BNM publishes 1700 rates after market close |

**DateOffset:** The 1700 session uses `DateOffset: -1` because it runs at midnight (next day), so it needs to look up yesterday's rates.

**Timezone:** All jobs use `Singapore Standard Time` (UTC+8) to align with BNM's market hours.

---

## 7. Audit Log Middleware — Code Explanation

### 7.1 Class Structure

```csharp
public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;           // Next middleware in pipeline
    private readonly ILogger<AuditLogMiddleware> _logger;     // For internal logging
    private readonly IServiceScopeFactory _scopeFactory;      // For background DB writes
}
```

- **`RequestDelegate _next`**: Standard ASP.NET Core middleware pattern — calls the next middleware in the pipeline.
- **`ILogger`**: Logs middleware's own operational events (e.g., "failed to save audit log").
- **`IServiceScopeFactory`**: Creates a new DI scope for the background database write. Cannot use the request-scoped `AppDbContext` because the request scope may be disposed before the background task completes.

### 7.2 Request Processing Flow

```
InvokeAsync(HttpContext)
    │
    ├─ 1. Check SkipPaths → Skip if /swagger, /hangfire, etc.
    │
    ├─ 2. EnableBuffering() → Allow body to be read multiple times
    │
    ├─ 3. Capture request data:
    │      ├─ HttpMethod, Endpoint, QueryString (sanitized)
    │      ├─ ClientIp (X-Forwarded-For → RemoteIpAddress → "unknown")
    │      ├─ UserAgent, TraceId
    │      ├─ Selected headers (4 safe headers only)
    │      └─ Request body (sanitized, truncated to 4KB)
    │
    ├─ 4. Swap response body stream with MemoryStream
    │
    ├─ 5. Start Stopwatch
    │
    ├─ 6. await _next(context) → Execute rest of pipeline
    │
    ├─ 7. Stop Stopwatch
    │
    ├─ 8. Capture response body (only if non-200)
    │
    ├─ 9. Copy MemoryStream back to original response stream
    │
    ├─ 10. Build AuditLog entity
    │
    └─ 11. Fire-and-forget: Task.Run → new scope → save to DB
```

### 7.3 Key Methods Explained

#### `CaptureHeaders(IHeaderDictionary headers)`
Iterates through the predefined safe header list (`Content-Type`, `Accept`, `User-Agent`, `X-Forwarded-For`) and serializes found headers to JSON. Headers not in the whitelist are ignored — this prevents accidentally logging `Authorization: Bearer <token>` or `Cookie` values.

#### `CaptureRequestBodyAsync(HttpRequest request)`
- Seeks to beginning of the buffered body stream
- Reads the entire body as UTF-8 string
- Seeks back to beginning (so the controller can read it again)
- Passes through `SanitizeBody()` and `TruncateString()`

#### `CaptureResponseBodyAsync(MemoryStream responseBodyStream)`
- Seeks to beginning of the captured response memory stream
- Reads as string
- Truncates to 4KB

#### `SanitizeBody(string? body)`
- Parses the body as JSON
- For each property, checks if the property name contains any sensitive field name (case-insensitive)
- Matching properties get value replaced with `***REDACTED***`
- Non-JSON bodies pass through unchanged (caught by `JsonDocument.Parse` exception)

#### `SanitizeQueryString(string queryString)`
- Splits query string into key-value pairs
- Checks each key against sensitive field list
- Matching keys get value replaced with `***REDACTED***`

### 7.4 Fire-and-Forget Pattern Explained

```csharp
_ = Task.Run(async () =>
{
    try
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "...");
    }
});
```

| Aspect | Design Decision | Reason |
|--------|----------------|--------|
| `Task.Run` | Offloads to thread pool | API response is not delayed by database write. Audit logging adds 0ms to response time. |
| `_ = Task.Run` | Discarded awaitable | We intentionally don't await — the response should already be sent to the client. |
| `_scopeFactory.CreateScope()` | New DI scope | The original request scope will be disposed after the response is sent. The background task needs its own scope with its own `AppDbContext` instance. |
| `try/catch` inside Task.Run | Fault isolation | Database failures (connectivity, disk full, deadlock) must never crash the API. The error is logged (to console/file) and the request succeeds normally. |

### 7.5 Response Body Stream Swap

```csharp
var originalBodyStream = context.Response.Body;
using var responseBodyStream = new MemoryStream();
context.Response.Body = responseBodyStream;

// ... execute pipeline (writes to MemoryStream) ...

responseBodyStream.Seek(0, SeekOrigin.Begin);
await responseBodyStream.CopyToAsync(originalBodyStream);
context.Response.Body = originalBodyStream;
```

This is the standard ASP.NET Core pattern for capturing response bodies. The response body stream is normally write-only and forward-only. By swapping it with a `MemoryStream`, we can:
1. Let the controller write the response to our MemoryStream
2. Read back the MemoryStream contents for audit logging
3. Copy the MemoryStream to the original stream so the client receives the response

---

## Appendix: Entity Relationship

```
┌─────────────────┐     ┌──────────────────────┐
│    Currency      │     │  ExchangeRateHistory  │
├─────────────────┤     ├──────────────────────┤
│ CurrencyId (PK) │◄────│ CurrencyId (FK)       │
│ CurrencyCode    │     │ CurrencyCode          │
│ CurrencyName    │     │ RateDate              │
│ UnitBase        │     │ Session               │
│ CreatedOn       │     │ BuyingRate            │
│ CreatedBy       │     │ SellingRate           │
│ ModifiedOn      │     │ MiddleRate            │
│ ModifiedBy      │     │ EffectiveDate         │
│ IsDeleted       │     │ CreatedOn / CreatedBy │
└─────────────────┘     │ ModifiedOn/ModifiedBy │
                        │ IsDeleted             │
                        └──────────────────────┘

┌──────────────────────┐     ┌───────────────────────┐
│      AuditLog         │     │    ApplicationLog      │
├──────────────────────┤     ├───────────────────────┤
│ Id (PK)              │     │ Id (PK)               │
│ TraceId  ◄──────────────────► TraceId               │
│ HttpMethod           │     │ Message               │
│ Endpoint             │     │ Level                 │
│ QueryString          │     │ TimeStamp             │
│ RequestHeaders       │     │ Exception             │
│ RequestBody          │     │ LogEvent              │
│ ResponseStatusCode   │     │ SourceContext          │
│ ResponseBody         │     └───────────────────────┘
│ ClientIpAddress      │
│ DurationMs           │
│ CreatedOn            │
└──────────────────────┘
     ▲ TraceId links both tables
```
