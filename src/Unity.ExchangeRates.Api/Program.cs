using AspNetCoreRateLimit;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Hangfire;
using Serilog;
using Unity.ExchangeRates.Infrastructure;
using Unity.ExchangeRates.Service;
using Unity.ExchangeRates.Shared;
using Unity.ExchangeRates.Shared.Jobs;
using Unity.ExchangeRates.Api.Configurations.Logging;
using Unity.ExchangeRates.Api.Configurations.Swagger;
using Unity.ExchangeRates.Api.Middlewares;

// ============================================================
// Bootstrap
// ============================================================
var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Configure Serilog 
// ============================================================
ConfigureLog(builder.Host);

// ============================================================
// Register all services - multi-layer pattern
// ============================================================
builder.Services.RegisterServiceModule(builder.Configuration);          // Service layer (Mediator, validators, BnmApiOptions)
builder.Services.RegisterInfrastructureModule(builder.Configuration);   // Infrastructure (EF, repositories)
builder.Services.RegisterSharedServiceModule(builder.Configuration);    // Shared (Hangfire, HTTP clients)

// Mediator source generator registration (must be in Api/startup)
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Controllers
builder.Services.AddControllers();

// API Versioning
ConfigureApiVersioning(builder.Services);

// Rate Limiting
ConfigureRateLimit(builder.Services, builder.Configuration);

// Swagger
ConfigureSwagger(builder.Services);

// ============================================================
// Build the application
// ============================================================
var app = builder.Build();

// rewrites RemoteIpAddress/Scheme before any middleware reads them
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseIpRateLimiting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions.Reverse())
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());
        }
    });

    app.UseHangfireDashboard();
}

app.UseHttpsRedirection();
app.UseMiddleware<AuditLogMiddleware>();
app.UseAuthorization();
app.MapControllers();

// ============================================================
// Configure recurring Hangfire jobs from appsettings (per BNM session)
// ============================================================
var exchangeRateSyncSection = app.Configuration.GetSection("HangfireJobs:ExchangeRateSync");
var exchangeRateSyncEnabled = exchangeRateSyncSection.GetValue<bool?>("Enabled") ?? true;

if (exchangeRateSyncEnabled)
{
    var timeZoneId = exchangeRateSyncSection.GetValue<string>("TimeZoneId") ?? "Local";
    TimeZoneInfo timeZone;

    if (string.Equals(timeZoneId, "Local", StringComparison.OrdinalIgnoreCase))
    {
        timeZone = TimeZoneInfo.Local;
    }
    else
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            app.Logger.LogWarning("Hangfire timezone '{TimeZoneId}' not found or invalid. Falling back to local timezone.", timeZoneId);
            timeZone = TimeZoneInfo.Local;
        }
    }

    var sessions = exchangeRateSyncSection.GetSection("Sessions").GetChildren();
    foreach (var sessionConfig in sessions)
    {
        var session = sessionConfig.GetValue<string>("Session")!;
        var jobId = sessionConfig.GetValue<string>("JobId") ?? $"exchange-rate-sync-{session}";
        var cronExpression = sessionConfig.GetValue<string>("Cron")!;
        var dateOffset = sessionConfig.GetValue<int?>("DateOffset") ?? 0;

        RecurringJob.AddOrUpdate<IExchangeRateSyncJob>(
            jobId,
            job => job.SyncSessionAsync(session, dateOffset, CancellationToken.None),
            cronExpression,
            new RecurringJobOptions { TimeZone = timeZone });

        app.Logger.LogInformation("Registered Hangfire job '{JobId}' for session {Session} (cron: {Cron}, dateOffset: {DateOffset})",
            jobId, session, cronExpression, dateOffset);
    }
}
else
{
    app.Logger.LogInformation("Hangfire ExchangeRateSync jobs are disabled via configuration.");
}

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
// API Versioning Configuration 
// ============================================================
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

// ============================================================
// Swagger Configuration (versioned docs)
// ============================================================
static void ConfigureSwagger(IServiceCollection services)
{
    services.AddEndpointsApiExplorer();

    services.AddSwaggerGen();
    services.ConfigureOptions<ConfigureSwaggerOptions>();
}

// ============================================================
// Rate Limiting Configuration 
// ============================================================
static void ConfigureRateLimit(IServiceCollection services, IConfiguration configuration)
{
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(configuration.GetSection(nameof(IpRateLimitOptions)));
    services.Configure<IpRateLimitPolicies>(configuration.GetSection(nameof(IpRateLimitPolicies)));
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
}

// ============================================================
// Logging Configuration
// ============================================================
static void ConfigureLog(IHostBuilder hostBuilder)
{
    hostBuilder.UseSerilog((context, config) =>
    {
        config.ReadFrom.Configuration(context.Configuration);
        config.Enrich.WithMethodName();
    });
}
