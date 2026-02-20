# Unity.ExchangeRates.svc

## Getting Started
Use these instructions to get the project up and running.

## Technologies
* .NET 9
* AutoMapper
* EF Core
* FluentResults
* FluentValidation
* Hangfire
* Mediator (Source Generator)
* Newtonsoft.Json
* Polly
* Serilog
* Swashbuckle (Swagger)

## Architecture
Clean Architecture with CQRS pattern.

```
src/
-- Unity.ExchangeRates.Api              # 4-Apps – Controllers, Middlewares, ViewModels, Program.cs
-- Unity.ExchangeRates.Domain           # 1-Domain – Entities, Exceptions
-- Unity.ExchangeRates.Repository       # 2-Repository – Repository interfaces
-- Unity.ExchangeRates.Service          # 3-Service – Commands, Queries, Validators, Behaviors, Errors
-- Unity.ExchangeRates.Infrastructure   # 4-Infrastructure – EF DbContext, Repository implementations, Interceptors
-- Unity.ExchangeRates.Shared           # 4-Cross Cutting – Hangfire jobs, HTTP clients, Polly policies
```

## Setup
Follow these steps to get your development environment set up:

  1. Clone the repository
  2. At the root directory, restore required packages by running:
      ```
     dotnet restore
     ```
  3. Next, build the solution by running:
     ```
     dotnet build
     ```
  4. Within the `\src\Unity.ExchangeRates.Api` directory, launch the API by running:
     ```
	 dotnet run
	 ```

## Docker
1. docker build
   ```
   docker build -f .\src\Unity.ExchangeRates.Api\Dockerfile -t <image-name>:<tag-name> .
   ```
2. docker run
   ```
   docker run --name <container-name> -d -p <host-machine-port>:<docker-port> <image-name>:<tag-name> -e "ENVIRONMENT=Development"
   ```

## Endpoints
### Swagger (dev only)
* /swagger
### Hangfire Dashboard
* /hangfire
### API
* GET `/api/exchangerates/{currency}/{date}` – Get exchange rate from BNM
* POST `/api/exchangerates/sync` – Sync exchange rates for all active currencies

## Learning Resources
