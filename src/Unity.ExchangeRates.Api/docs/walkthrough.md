# Walkthrough — API Versioning & API Key Authentication

## Changes Made

| File | Change |
|------|--------|
| [Unity.ExchangeRates.Api.csproj](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Unity.ExchangeRates.Api.csproj) | Added `Asp.Versioning.Mvc` 8.1.0, `ApiExplorer` 8.1.0. Downgraded Swashbuckle to 8.1.1 |
| [Program.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Program.cs) | Added `ConfigureApiVersioning()`, `ConfigureSwagger()`, registered [ApiKeyAuthMiddleware](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Middlewares/ApiKeyAuthMiddleware.cs#6-70) |
| [ExchangeRateController.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Controllers/ExchangeRateController.cs) | `[ApiVersion("1.0")]` + route `api/v{version:apiVersion}/exchangerates` |
| [ApiKeyAuthMiddleware.cs](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/Middlewares/ApiKeyAuthMiddleware.cs) | **NEW** — `X-Api-Key` validation middleware |
| [appsettings.Development.json](file:///c:/Users/soleh/source/repos/Unity.ExchangeRates.svc/src/Unity.ExchangeRates.Api/appsettings.Development.json) | Added `ApiSecurity.ApiKey` |

## Test Results

### ✅ Versioned endpoints show `/api/v1/...`

![Swagger showing versioned GET endpoint at /api/v1/exchangerates](file:///C:/Users/soleh/.gemini/antigravity/brain/0ae1271e-6971-4c17-ab8a-bc5da8630e5a/swagger_versioned_endpoint.png)

### ✅ Authorize button with X-Api-Key

![Swagger Authorize button for API key](file:///C:/Users/soleh/.gemini/antigravity/brain/0ae1271e-6971-4c17-ab8a-bc5da8630e5a/swagger_authorize_button.png)

### ✅ 401 WITHOUT API key
```json
{
  "status": "Failed",
  "errorCode": "00401",
  "errorMsg": "API Key is required. Provide it via the X-Api-Key header.",
  "timestamp": "2026-02-28T16:04:08.265+08:00"
}
```

### ✅ 200 WITH valid API key

![200 OK response with exchange rate data after providing valid API key](file:///C:/Users/soleh/.gemini/antigravity/brain/0ae1271e-6971-4c17-ab8a-bc5da8630e5a/swagger_200_with_key.png)

### 🎬 Full Testing Recording

![Swagger API testing recording](file:///C:/Users/soleh/.gemini/antigravity/brain/0ae1271e-6971-4c17-ab8a-bc5da8630e5a/swagger_api_testing_1772265688524.webp)
