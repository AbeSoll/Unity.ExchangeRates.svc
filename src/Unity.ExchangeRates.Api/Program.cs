using AspNetCoreRateLimit;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Hangfire;
using Mediator;
using Microsoft.OpenApi.Models;
using Serilog;
using Unity.ExchangeRates.Infrastructure;
using Unity.ExchangeRates.Service;
using Unity.ExchangeRates.Shared;
using Unity.ExchangeRates.Shared.Jobs;
using Unity.ExchangeRates.Api.Configurations;
using Unity.ExchangeRates.Api.Configurations.Logging;
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

// CORS
ConfigureCors(builder.Environment, builder.Services, builder.Configuration);

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
app.UseCors();
app.UseAuditLog();
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

    var serviceProvider = services.BuildServiceProvider();
    var apiVersionDescriptionProvider = serviceProvider.GetRequiredService<IApiVersionDescriptionProvider>();

    services.AddSwaggerGen(swagger =>
    {
        foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
        {
            var apiInfo = new OpenApiInfo
            {
                Title = "Unity Exchange Rates API",
                Version = $"{description.ApiVersion}"
            };
            if (description.IsDeprecated)
            {
                apiInfo.Description += " This API version has been deprecated.";
            }
            swagger.SwaggerDoc(description.GroupName, apiInfo);
        }
    });
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
