using Hangfire;
using Mediator;
using Serilog;
using Unity.ExchangeRates.Infrastructure;
using Unity.ExchangeRates.Service;
using Unity.ExchangeRates.Shared;
using Unity.ExchangeRates.Shared.Jobs;
using Unity.ExchangeRates.Api.Configurations;
using Unity.ExchangeRates.Api.Middlewares;

// ============================================================
// Bootstrap Serilog first so startup errors are captured
// ============================================================
var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// ============================================================
// Register all services — multi-layer pattern
// ============================================================
builder.Services.RegisterServiceModule(builder.Configuration);          // Service layer (Mediator, validators, BnmApiOptions)
builder.Services.RegisterInfrastructureModule(builder.Configuration);   // Infrastructure (EF, repositories)
builder.Services.RegisterSharedServiceModule(builder.Configuration);    // Shared (Hangfire, HTTP clients)

// Mediator source generator registration (must be in Api/startup)
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// CORS
ConfigureCors(builder.Environment, builder.Services, builder.Configuration);

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// Build the application
// ============================================================
var app = builder.Build();

app.UseMiddleware<ExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseHangfireDashboard();
app.UseAuthorization();
app.MapControllers();

// ============================================================
// Configure recurring Hangfire job: daily at 00:00 local time
// ============================================================
RecurringJob.AddOrUpdate<IExchangeRateSyncJob>(
    "daily-exchange-rate-sync",
    job => job.SyncDailyAsync(CancellationToken.None),
    "0 0 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

// ============================================================
// Run
// ============================================================
try
{
    Log.Information("Exchange Rates API starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// ============================================================
// CORS Configuration 
// ============================================================
static void ConfigureCors(IWebHostEnvironment environment, IServiceCollection services, IConfiguration configuration)
{
    var corsOptions = configuration.GetSection(nameof(CorsOptions)).Get<CorsOptions>();
    if (corsOptions == null)
    {
        if (environment.IsDevelopment())
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                });
            });
        }
        else
        {
            throw new InvalidOperationException("Cors is not configured correctly in appsettings.");
        }
    }
    else
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.WithOrigins(corsOptions.Origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            });
        });
    }
}