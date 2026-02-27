# Unity Exchange Rates API — Full Demo Script

> **How to use this script:** Read each section out loud in a casual, confident tone. The 📂 icon tells you which file to open and show on screen. The 🗣️ icon is what you say. The 💡 icon is key points to highlight.

---

## PART 1 — Opening & Big Picture (2 min)

🗣️ *"So today I want to walk you through the Unity Exchange Rates API that I've been building. Let me start with the big picture of what this service does."*

🗣️ *"This API serves one main purpose — it provides exchange rate data for Life Asia. Every day, it automatically fetches the latest currency exchange rates from Bank Negara Malaysia's open API, stores them in our database, and then exposes endpoints for Life Asia to query those rates."*

🗣️ *"There are two main flows:*
1. *An automated daily job that fetches rates at midnight*
2. *A REST API that lets users query stored exchange rates by currency and date"*

💡 **Key point to emphasize:** This is a **fully automated** system. Once deployed, it runs itself. The API is just for users to retrieve the data that's already been synced.

---

## PART 2 — Project Architecture & Folder Structure (3 min)

📂 **Open: Solution Explorer — show all 6 projects**

🗣️ *"I've structured this project using a layered architecture pattern. It's the same pattern we use in the Facility service. Let me go through each layer and what it's responsible for."*

🗣️ *"We have six separate projects, and each one has a clear responsibility:"*

| Layer | Responsibility |
|-------|---------------|
| **Api** | The entry point — controllers, middleware, Swagger |
| **Domain** | Our data models and entities — pure C# classes, no dependencies |
| **Repository** | Only interfaces — defines the contracts for data access |
| **Infrastructure** | The concrete implementations — EF Core, database context, actual repository code |
| **Service** | Business logic — CQRS handlers, validators, the core brain of the app |
| **Shared** | Cross-cutting concerns — Hangfire jobs, HTTP client setup |

🗣️ *"The key design principle here is the direction of dependency. The inner layers like Domain and Repository have zero dependencies on outer layers. Infrastructure implements the Repository interfaces. Service contains the business logic. And Api ties everything together."*

💡 **If asked "why separate Repository and Infrastructure?":** *"Repository holds only interfaces — it's like a contract. Infrastructure provides the actual implementation. This separation means we can swap out the database or mock it in tests without touching business logic."*

---

## PART 3 — Entry Point: Program.cs (3 min)

📂 **Open:** [Program.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Program.cs)

🗣️ *"Let me start from the entry point of the application — Program.cs. This is where everything gets wired up."*

**Scroll to the top section (lines 1-10):**

🗣️ *"First, we configure Serilog for structured logging. This gives us proper log output with timestamps, log levels, and method names."*

**Scroll to the service registration section (lines 24-30):**

```csharp
builder.Services.RegisterServiceModule(builder.Configuration);
builder.Services.RegisterInfrastructureModule(builder.Configuration);
builder.Services.RegisterSharedServiceModule(builder.Configuration);
```

🗣️ *"Here's where the multi-layer pattern comes together. Each layer has its own registration method. The Service module registers the Mediator pipeline and validators. Infrastructure registers EF Core and repositories. Shared registers Hangfire and the HTTP client for BNM API."*

🗣️ *"We also register AutoMapper and Mediator here at the Api level because they need to be in the startup assembly."*

**Scroll to the middleware section (line 46):**

```csharp
app.UseMiddleware<ExceptionHandlerMiddleware>();
```

🗣️ *"We have a custom exception handler middleware. This catches any unhandled exception across the entire application and returns a proper JSON error response instead of a 500 crash page."*

**Scroll to the Hangfire section (lines 60-64):**

```csharp
RecurringJob.AddOrUpdate<IExchangeRateSyncJob>(
    "daily-exchange-rate-sync",
    job => job.SyncDailyAsync(CancellationToken.None),
    "0 0 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
```

🗣️ *"This is the Hangfire recurring job configuration. The cron expression `0 0 * * *` means it runs every day at midnight — 12:00 AM. The TimeZone is set to local so it follows our server time. This is what drives the automatic daily sync."*

💡 **Key point:** *"This runs every single day — including weekends. We handle the weekend logic inside the business logic, which I'll show you shortly."*

---

## PART 4 — Domain Models (2 min)

📂 **Open:** [BaseEntity.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Domain/Models/BaseEntity.cs)

🗣️ *"Let me quickly show the domain models. We have a BaseEntity that all our entities inherit from. It provides common fields like `Id`, `CreatedOn`, `CreatedBy`, `ModifiedOn`, `ModifiedBy`, and `IsDeleted` — standard audit fields."*

📂 **Open:** [Currency.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Domain/Models/Currency.cs)

🗣️ *"The Currency model represents currencies we track — like USD, GBP, SGD. It has a `CurrencyCode` as the primary key, a `CurrencyName`, and `UnitBase`. These are pre-populated in the database."*

📂 **Open:** [ExchangeRateHistory.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Domain/Models/ExchangeRateHistory.cs)

🗣️ *"This is the main table — ExchangeRateHistory. Every time we sync from BNM, a new row goes here. It has:"*
- *"`CurrencyCode` — which currency, like USD"*
- *"`RateDate` — the actual BNM business date for the rate"*
- *"`BuyingRate`, `SellingRate`, `MiddleRate` — the three rates BNM provides"*
- *"`EffectiveDate` — when this rate is effective"*
- *"And since it inherits BaseEntity, we also get `CreatedOn` — which is when our system fetched this data"*

💡 **Key point:** *"The difference between `RateDate` and `CreatedOn` is important. `RateDate` is BNM's date. `CreatedOn` is when we stored it. Users query by `CreatedOn` to get the rate available on a specific day."*

---

## PART 5 — The CQRS Pattern (2 min)

🗣️ *"Before I show the controllers, let me briefly explain the pattern we use — CQRS, which stands for Command Query Responsibility Segregation."*

🗣️ *"The idea is simple — we separate read operations from write operations:"*
- *"**Queries** = reading data from the database (GET endpoint)"*
- *"**Commands** = writing data / triggering actions (POST sync endpoint)"*

🗣️ *"We use the Mediator library to implement this. The controller doesn't call the database directly. Instead, it creates a query or command object and sends it through the Mediator. The Mediator finds the right handler and executes it."*

🗣️ *"Why is this good? Because each handler is a small, focused class that does one thing. It's easy to test, easy to debug, and easy to add new features without touching existing code."*

---

## PART 6 — Controller (3 min)

📂 **Open:** [ExchangeRateController.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Controllers/ExchangeRateController.cs)

🗣️ *"Here's our controller — it's intentionally thin. It has two endpoints."*

**Point to the constructor (lines 19-24):**

🗣️ *"The controller gets three dependencies injected — AutoMapper for mapping objects, Mediator for sending commands and queries, and a logger."*

**Point to GetRate method (lines 26-34):**

```csharp
[HttpGet("{currency}/{date}")]
public async Task<IActionResult> GetRate(string currency, string date)
```

🗣️ *"The GET endpoint takes a currency code and date from the URL. For example: `/api/exchangerates/usd/2026-02-26`. It maps the input to a Query object, sends it through Mediator, and returns the result."*

**Point to Sync method (lines 36-44):**

```csharp
[HttpPost("sync")]
public async Task<IActionResult> Sync([FromBody] ExchangeRateSyncRequest syncRequest)
```

🗣️ *"The POST endpoint is for manual sync. It takes a JSON body with `date` and optionally `session`. It maps to a Command object and sends it through Mediator."*

🗣️ *"Notice how both methods follow the same pattern — map the request, send through Mediator, return the response. The controller doesn't contain any business logic at all. It's just a bridge between HTTP and our business layer."*

---

## PART 7 — AutoMapper Profiles (1 min)

📂 **Open:** [InitialMapper.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Configurations/InitialMapper.cs)

🗣️ *"AutoMapper handles the mapping between different object types. We have three mappings:"*

```csharp
CreateMap<ExchangeRateRequest, ExchangeRateQuery>();       // GET → Query
CreateMap<ExchangeRateSyncRequest, ExchangeRateSyncCommand>(); // POST → Command
CreateMap<BaseResult, BaseResponse>();                     // Result → Response
```

🗣️ *"Since the property names match between source and destination, AutoMapper automatically maps them. This keeps our controller code clean."*

---

## PART 8 — Validation Pipeline (2 min)

📂 **Open:** [RequestValidationBehavior.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Service/Behaviors/RequestValidationBehavior.cs)

🗣️ *"Before any command or query reaches its handler, it goes through our validation pipeline. This is a Mediator pipeline behavior — think of it like middleware, but for Mediator."*

🗣️ *"What happens here is: when a request comes in, this behavior collects all the validators registered for that request type, runs them, and if any fail, it short-circuits the pipeline and returns a validation error. The request never reaches the handler."*

📂 **Open:** [ExchangeRateSyncCommandValidator.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandValidator.cs)

🗣️ *"Here's a concrete validator for the Sync command. It validates two things:"*
1. *"`date` must not be empty and must be in `yyyy-MM-dd` format"*
2. *"`session` — if provided — must be one of the valid BNM sessions: 0900, 1130, 1200, or 1700"*

🗣️ *"If someone sends an invalid date format or an invalid session through Swagger, they'll get a clear 400 Bad Request with a specific error message."*

---

## PART 9 — Query Handler (GET flow) (3 min)

📂 **Open:** [ExchangeRateQueryHandler.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Service/Mediator/Queries/ExchangeRates/ExchangeRateQueryHandler.cs)

🗣️ *"Now let's trace the GET flow — what happens when a user wants to retrieve an exchange rate."*

🗣️ *"This handler receives the query with `currency` and `date`. Here's the flow:"*

**Point to lines 28-33:**

🗣️ *"First, it logs the incoming query at Debug level — this is for development troubleshooting."*

🗣️ *"Then it parses the date string into a DateTime and calls the repository to find the rate. Notice we're querying by `CreatedOn.Date` — meaning we find the rate that was stored on the date the user specified."*

**Point to lines 35-45:**

🗣️ *"If no rate is found — maybe the sync didn't run or failed for that date — we return a 404 Not Found with a clear error message. This is logged at Warning level because it's unexpected but not an error."*

🗣️ *"If the rate is found, we log success at Information level and return the data."*

**Point to the catch block (lines 53-56):**

🗣️ *"If anything unexpected crashes — like a database connection timeout — the catch block logs it as Error and returns a 500 with the error message."*

💡 **Key point:** *"In practice, users will query with today's date. Today's data was synced at midnight, so it should always be available. If it's not, the Warning log tells us something went wrong with the Hangfire job."*

---

## PART 10 — Command Handler (POST / Sync flow) — the core business logic (5 min)

📂 **Open:** [ExchangeRateSyncCommandHandler.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Service/Mediator/Commands/ExchangeRates/ExchangeRateSyncCommandHandler.cs)

🗣️ *"This is the heart of the application — where the main business logic lives. Let me walk through it step by step."*

**Point to lines 37-41 (date parsing + resolve):**

```csharp
var inputDate = DateTime.ParseExact(request.date!, "yyyy-MM-dd", ...);
var targetDate = ResolveBusinessDate(inputDate);
var targetDateStr = targetDate.ToString("yyyy-MM-dd");
var session = !string.IsNullOrEmpty(request.session) ? request.session : _settings.DefaultSession;
```

🗣️ *"Step 1 — we parse the input date and then run it through `ResolveBusinessDate()`. This is a critical function."*

**Scroll down to ResolveBusinessDate (bottom of file):**

```csharp
private static DateTime ResolveBusinessDate(DateTime date)
{
    while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        date = date.AddDays(-1);
    return date;
}
```

🗣️ *"BNM doesn't publish rates on weekends. So if the input date falls on Saturday or Sunday, we keep subtracting one day until we reach Friday. For example:"*
- *"Saturday → resolves to Friday"*
- *"Sunday → also resolves to Friday"*

🗣️ *"This means on weekends and on Monday (because Hangfire sends yesterday's date which is Sunday), we always fetch Friday's rate. This is by requirement — the system must have a rate for every single day."*

**Scroll back up, point to the session line:**

🗣️ *"For the session, developers can choose which BNM session to use — 0900, 1130, 1200, or 1700. If not specified, it defaults to 1700 from our appsettings config. The daily automated job always uses 1700 because we want the end-of-day rate."*

**Point to lines 48-51 (load currencies + begin transaction):**

🗣️ *"Next, we load all active currencies from the database — these are the currencies we're tracking. Then we begin a database transaction. This is important because we want all-or-nothing — either all currencies sync successfully, or we rollback."*

**Point to the foreach loop (lines 53-97):**

🗣️ *"Then we loop through each currency and for each one:"*

1. *"Build the BNM API URL with the currency, date, session, and quote=rm"*
2. *"Call the BNM API"*
3. *"If the API returns an error (like 404 for a specific currency), we log it and skip to the next currency — we don't fail the whole batch"*
4. *"If successful, we deserialize the BNM response into our `BnmApiResponse` model"*
5. *"Create an `ExchangeRateHistory` entity with the rates — BuyingRate, SellingRate, MiddleRate"*
6. *"Add it to the database context"*

**Point to lines 98-102 (save + commit):**

🗣️ *"After all currencies are processed, we save everything to the database and commit the transaction. We log how many currencies were synced out of the total."*

**Point to the catch block (lines 104-109):**

🗣️ *"If anything goes wrong during the entire process — maybe the database is down — the catch block rolls back the transaction so we don't end up with partial data. The error is logged and returned."*

💡 **Key point to emphasize:** *"The transaction + rollback pattern ensures data integrity. We never have a situation where only some currencies are synced and others aren't."*

---

## PART 11 — Hangfire Job (2 min)

📂 **Open:** [ExchangeRateSyncJob.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Shared/Jobs/ExchangeRateSyncJob.cs)

🗣️ *"This is the Hangfire job that gets triggered every day at midnight. It's quite simple."*

```csharp
var now = DateTime.Now;
var yesterday = now.Date.AddDays(-1).ToString("yyyy-MM-dd");
var command = new ExchangeRateSyncCommand { date = yesterday };
var result = await _mediator.Send(command, cancellationToken);
```

🗣️ *"The job calculates yesterday's date, creates a sync command with that date, and sends it through Mediator. The same command handler we just looked at processes it."*

🗣️ *"Why yesterday? Because the job runs at midnight — 12 AM. At that point, BNM has already published the 5 PM rate for the previous day. So we're taking yesterday's 1700 session rate."*

🗣️ *"Let me trace through the whole week to show how this works:"*

| Job runs at | Yesterday (-1 day) | ResolveBusinessDate | BNM rate fetched | User queries |
|---|---|---|---|---|
| **Tuesday 12AM** | Monday | Monday ✅ | Monday 1700 | Tuesday's date |
| **Wednesday 12AM** | Tuesday | Tuesday ✅ | Tuesday 1700 | Wednesday's date |
| **Saturday 12AM** | Friday | Friday ✅ | Friday 1700 | Saturday's date |
| **Sunday 12AM** | Saturday → resolves to | **Friday** | Friday 1700 | Sunday's date |
| **Monday 12AM** | Sunday → resolves to | **Friday** | Friday 1700 | Monday's date |
| **Tuesday 12AM** | Monday | Monday ✅ | Monday 1700 | Tuesday's date |

🗣️ *"So Saturday, Sunday, and Monday all get Friday's rate — which is expected because BNM doesn't publish on weekends. Each day still gets its own row in the database for traceability."*

💡 **If asked about duplicate rates:** *"Yes, Friday's rate appears in 3 rows (Saturday, Sunday, Monday). This is by design and by requirement. The system needs to run every day, and each day's sync creates its own record. Life Asia always gets a rate regardless of the day."*

---

## PART 12 — Repository & Database Layer (3 min)

📂 **Open:** [IExchangeRateRepository.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Repository/IExchangeRateRepository.cs)

🗣️ *"The repository layer defines three operations as interfaces:"*
1. *"`GetActiveCurrenciesAsync` — fetches all currencies we track"*
2. *"`GetRateByCreatedDateAsync` — finds a rate by currency code and the date it was created in our system"*
3. *"`AddRateHistoryAsync` — inserts a new rate record"*

📂 **Open:** [ExchangeRateRepository.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Infrastructure/Repositories/ExchangeRateRepository.cs)

🗣️ *"And here's the concrete implementation using Entity Framework. For example, `GetRateByCreatedDateAsync` queries the ExchangeRateHistory table matching on CurrencyCode and where `CreatedOn.Date` equals the requested date."*

📂 **Open:** [UnitOfWork.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Infrastructure/UnitOfWork.cs)

🗣️ *"We also use the Unit of Work pattern. This wraps the repository and database context together, providing transaction management — `BeginTransaction`, `Commit`, and `Rollback`. This is what the command handler uses to ensure data integrity."*

📂 **Open:** [AppDbContext.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Infrastructure/Data/AppDbContext.cs)

🗣️ *"The DbContext defines our two tables — `Currencies` and `ExchangeRateHistories` — and configures the column types and relationships using Fluent API."*

📂 **Open:** [EntitySaveChangeInterceptor.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Infrastructure/Interceptors/EntitySaveChangeInterceptor.cs)

🗣️ *"We also have a SaveChanges interceptor. Every time EF saves data, this interceptor automatically sets `CreatedOn` for new records and `ModifiedOn` for updated records. This ensures audit fields are always populated without developers having to remember to set them manually."*

---

## PART 13 — HTTP Client & Resilience (2 min)

📂 **Open:** [ServiceCollectionExtensions.cs (Shared)](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Shared/ServiceCollectionExtensions.cs)

🗣️ *"The BNM API client is configured using the HTTP client factory pattern. Let me highlight a few things."*

**Point to the HttpClient setup:**

🗣️ *"The base URL and Accept header come from appsettings — `BnmApiSettings` section. The timeout is set to 10 seconds per request."*

**Point to BuildRetryPolicy:**

```csharp
.AddPolicyHandler(BuildRetryPolicy());
```

🗣️ *"We use Polly for resilience. If the BNM API call fails due to a transient error — like a network timeout or a 503 — it will automatically retry 3 times with increasing delays: 1 second, then 2 seconds, then 5 seconds. This makes our system resilient to temporary BNM API issues."*

---

## PART 14 — Middleware & Error Handling (2 min)

📂 **Open:** [ExceptionHandlerMiddleware.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Middlewares/ExceptionHandlerMiddleware.cs)

🗣️ *"Our global exception middleware catches any exception that wasn't handled by the application logic. It categorizes them:"*

- *"**Domain exceptions** (like business rule violations) → 400 Bad Request, logged as Warning since it's expected"*
- *"**Validation exceptions** → 400 Bad Request, logged as Warning"*
- *"**Everything else** (unexpected crashes) → 500 Internal Server Error, logged as Error"*

🗣️ *"This ensures the API always returns a proper JSON response, never an ugly stack trace, regardless of what goes wrong."*

---

## PART 15 — Logging Strategy (2 min)

🗣️ *"Let me briefly explain our logging strategy. We use Serilog with structured logging across the entire application. Each log level has a specific purpose:"*

| Level | When we use it | Example |
|-------|---------------|---------|
| **Debug** | Internal operations, dev-only detail | *"Repository: GetActiveCurrenciesAsync called"* |
| **Information** | Key business flow milestones | *"Sync completed. Synced 8/8 currencies"* |
| **Warning** | Unexpected but recoverable situations | *"No rate found for USD on 2026-02-26"*, *"Transaction rolled back"* |
| **Error** | Operation failures that need attention | *"BNM API returned 404 for JPY"* |
| **Critical** | System-level crashes | *"Hangfire job crashed unexpectedly"* |

🗣️ *"Every handler, repository, and middleware follows this convention. In production, we can set the minimum log level to Information to reduce noise, and switch to Debug only when troubleshooting."*

---

## PART 16 — LIVE DEMO (5 min)

### Demo 1: Swagger

🗣️ *"Now let me show you the API running. This is the Swagger UI."*

1. **Show GET endpoint** — expand `/api/exchangerates/{currency}/{date}`
2. Enter `usd` and today's date `2026-02-26`
3. Click Execute
4. Show the JSON response with BuyingRate, SellingRate, MiddleRate

🗣️ *"The user fills in today's date and gets the rate that was automatically synced at midnight. The rate itself is from yesterday at 5PM session."*

### Demo 2: POST sync (manual)

1. **Show POST endpoint** — expand `/api/exchangerates/sync`
2. Enter body:
```json
{
  "date": "2026-02-25",
  "session": "1700"
}
```
3. Click Execute
4. Show response: *"Synced 8 of 8 currencies for 2026-02-25 (session=1700)"*

🗣️ *"This POST endpoint is mainly for developers. Say the midnight job failed, or we need to backfill historical data — we can manually trigger a sync for any date and session."*

### Demo 3: Hangfire Dashboard

1. Open Hangfire dashboard (usually at `/hangfire`)
2. Show **Recurring Jobs** tab — point to `daily-exchange-rate-sync` with cron `0 0 * * *`
3. Show **Succeeded** tab — recent job executions

🗣️ *"Here's the Hangfire dashboard. We can see our daily job scheduled to run at midnight. The Succeeded tab shows that it's been running successfully every day."*

### Demo 4: Database

1. Open SQL Server Management Studio or your DB tool
2. Show **Currency** table — list of currencies being tracked
3. Show **ExchangeRateHistory** table — recent rows
4. Point out: different `CreatedOn` dates but same `RateDate` for weekend records

🗣️ *"In the database, you can see the Currency table has our tracked currencies, and ExchangeRateHistory has all the synced rates. Notice these 3 rows — Saturday, Sunday, and Monday — all have the same RateDate of Friday, but different CreatedOn dates. That's the weekend logic working as expected."*

---

## PART 17 — Summary & Close (1 min)

🗣️ *"So to wrap up — the Unity Exchange Rates API is:"*

1. ✅ *"**Fully automated** — Hangfire syncs rates daily at midnight"*
2. ✅ *"**Resilient** — retry policy for BNM API, transaction rollback for data integrity"*
3. ✅ *"**Clean architecture** — 6 layers with clear separation of concerns"*
4. ✅ *"**CQRS pattern** — commands and queries are separated with proper validation"*
5. ✅ *"**Structured logging** — proper log levels for monitoring and debugging"*
6. ✅ *"**Weekend-aware** — automatically resolves to Friday's rate"*
7. ✅ *"**Configurable** — session, BNM API settings all driven by appsettings"*
8. ✅ *"**Ready for Life Asia** — data available every day, queryable by currency and date"*

🗣️ *"Any questions?"*

---

## ⚡ Quick Q&A Cheat Sheet

| Potential question | Answer |
|---|---|
| **Why not call BNM API in real-time when user queries?** | Performance and reliability. We cache in our DB so queries are instant. If BNM is down, our data is still available. |
| **What if the job fails?** | We have the POST endpoint for manual re-sync. Logs will show us exactly what failed. |
| **Why duplicate data on weekends?** | Requirement — each day needs a record. Life Asia expects a rate for every date. |
| **Why CQRS and not just simple services?** | Separation of concerns, easier to test, each handler is focused. Also aligns with Facility's architecture. |
| **What if BNM adds a new currency?** | We add it to the Currency table. Next sync will automatically include it. |
| **Why session is configurable?** | Different sessions give different rates (morning vs end-of-day). Developers can test with different sessions. |
