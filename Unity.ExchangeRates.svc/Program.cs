using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Unity.ExchangeRates.svc.Data;
using Unity.ExchangeRates.svc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// 1. REGISTER DATABASE CONTEXT
// Configures EF Core to use SQL Server with the connection string from appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. REGISTER SERVICES & TYPED HTTP CLIENT with Polly policies
var baseUrl = builder.Configuration["BnmApiConfig:BaseUrl"] ?? "https://api.bnm.gov.my/public/exchange-rate";

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    // Retry on transient errors (5xx, network failures) with exponential backoff
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5)
        });
}

builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
{
    // Ensure BaseAddress ends with a trailing slash so relative paths resolve correctly.
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.BNM.API.v1+json");
    // Optionally set a sensible timeout
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddPolicyHandler(GetRetryPolicy());

// Register Controllers
builder.Services.AddControllers();

// Swagger configuration for testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirect HTTP to HTTPS for security (Mandatory for BNM)
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();