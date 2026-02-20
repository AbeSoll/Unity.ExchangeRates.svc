# How CQRS Works in This Project

## What Is CQRS?

**CQRS** = **Command Query Responsibility Segregation** — split read operations (**Queries**) from write operations (**Commands**).

| Type | Purpose | Example in This API | Returns Data? |
|---|---|---|---|
| **Query** | Read / get data | Get exchange rate for USD on 2025-02-12 | Yes (rate from BNM API) |
| **Command** | Change state / write | Sync all currency rates for a date to database | Yes (summary message) |

---

## Implementation: Mediator Source-Generator Library

This project uses the **Mediator** source-generator NuGet package (not MediatR). It provides:

| Mediator Concept | Interface | Used In |
|---|---|---|
| **Request** | `IRequest<TResponse>` | Query and Command classes |
| **Handler** | `IRequestHandler<TRequest, TResponse>` | Handler classes |
| **Dispatcher** | `ISender` | Controllers and jobs call `_mediator.Send(request)` |
| **Pipeline** | `IPipelineBehavior<TRequest, TResponse>` | `RequestValidationBehavior` — runs FluentValidation before every handler |

The source generator runs at **compile time** to wire up all handlers automatically via DI. No runtime reflection.

---

## Request Flow (Step by Step)

### Query Flow: `GET /api/exchangerates/{currency}/{date}`

```
1. HTTP Request arrives
       ↓
2. ExchangeRateController.GetRate()
   - Maps ExchangeRateRequest → ExchangeRateQuery (via AutoMapper)
   - Calls _mediator.Send(query)
       ↓
3. RequestValidationBehavior (pipeline)
   - Finds ExchangeRateQueryValidator
   - Validates: currency not empty, date not empty, date format yyyy-MM-dd
   - If invalid → returns Result<BaseResult> with ValidationError (400)
       ↓
4. ExchangeRateQueryHandler.Handle()
   - Builds BNM API URL from BnmApiOptions
   - Calls _httpClient.GetAsync(url) — uses named "BnmClient" with Polly retry
   - If HTTP error → returns Result.Fail(GeneralError)
   - If null data → returns Result.Fail(NotFoundError)
   - If success → returns Result.Ok(BaseResult { data = BnmRateData })
       ↓
5. Controller receives Result<BaseResult>
   - Maps BaseResult → BaseResponse (via AutoMapper)
   - Calls ApiResponse<BaseResponse, BaseResult>() on BaseApiController
   - Success → 200 OK with response body
   - ValidationError → 400 Bad Request
   - NotFoundError → 404 Not Found
   - GeneralError → 400 Bad Request
```

---

### Command Flow: `POST /api/exchangerates/sync`

```
1. HTTP Request with JSON body { "appId": "...", "date": "2025-02-12" }
       ↓
2. ExchangeRateController.Sync()
   - Maps ExchangeRateSyncRequest → ExchangeRateSyncCommand (via AutoMapper)
   - Calls _mediator.Send(command)
       ↓
3. RequestValidationBehavior (pipeline)
   - Finds ExchangeRateSyncCommandValidator
   - Validates: date not empty, format yyyy-MM-dd
       ↓
4. ExchangeRateSyncCommandHandler.Handle()
   - Calls _repository.GetActiveCurrenciesAsync() — gets all Currency rows
   - For EACH currency:
       a. Creates ExchangeRateQuery { currency, date }
       b. Calls _mediator.Send(query) — reuses the Query handler internally
       c. If success → creates ExchangeRateHistory entity, calls _repository.AddRateHistoryAsync()
       d. If failed → logs warning, skips currency
   - Calls _repository.SaveChangesAsync() — bulk save
   - Returns BaseResult with "Synced X of Y currencies for date"
       ↓
5. Controller maps result → BaseResponse → HTTP 200 or error
```

> **Key insight:** The Sync command internally dispatches Query requests via `_mediator.Send()` to reuse the same BNM API call logic. This avoids code duplication.

---

### Hangfire Job Flow: Automated Daily Sync

```
1. Hangfire scheduler triggers at 00:00 local time (cron: "0 0 * * *")
       ↓
2. ExchangeRateSyncJob.SyncDailyAsync()
   - Calculates previous business day (skips weekends)
   - Creates ExchangeRateSyncCommand { date = previous business day }
   - Calls _mediator.Send(command)
       ↓
3. ExchangeRateSyncCommandHandler.Handle()
   - Same flow as the manual POST sync above
       ↓
4. Job logs success or failure
```

---

## Validation Pipeline

`RequestValidationBehavior<TRequest, TResponse>` is registered as an `IPipelineBehavior` and runs **before** every handler:

```csharp
public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken ct)
{
    // 1. Find all IValidator<TRequest> implementations
    // 2. Run all validators
    // 3. If any failures → return Result with ValidationError (short-circuit, handler never runs)
    // 4. If all pass → call next(message, ct) to invoke the actual handler
}
```

Validators are registered automatically via `services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())` in the Service layer's DI.

---

## Error Types (FluentResults)

All handlers return `Result<BaseResult>` from the **FluentResults** library. Errors implement `IError`:

| Error Type | HTTP Status | When Used |
|---|---|---|
| `ValidationError` | 400 | Input validation failure (via pipeline) |
| `GeneralError` | 400 | BNM API returned non-success status, or generic business error |
| `NotFoundError` | 404 | BNM API returned null/empty data |

Each error carries: `appId`, `status` ("Failed"), `timestamp`, `traceId`, `errorCode`, `errorMsg`.

`BaseApiController.ApiResponse()` inspects the error type and sets the correct HTTP status code.

---

## Summary

| Concept | What It Is | Where It Lives |
|---|---|---|
| **Query** | `ExchangeRateQuery : IRequest<Result<BaseResult>>` | `Service/Mediator/Queries/ExchangeRates/` |
| **Command** | `ExchangeRateSyncCommand : IRequest<Result<BaseResult>>` | `Service/Mediator/Commands/ExchangeRates/` |
| **Handler** | `ExchangeRateQueryHandler` / `ExchangeRateSyncCommandHandler` | Same folders as their query/command |
| **Validator** | `ExchangeRateQueryValidator` / `ExchangeRateSyncCommandValidator` | Same folders |
| **Pipeline** | `RequestValidationBehavior` | `Service/Behaviors/` |
| **Dispatcher** | `ISender` from Mediator library | Injected into controllers and jobs |
| **Result** | `Result<BaseResult>` from FluentResults | Success or failure with typed errors |

Controllers only create the query/command, send it through `ISender`, and map the result to HTTP. **All business logic lives in handlers.**
