using Unity.ExchangeRates.svc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Swagger configuration for testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient for the BNM OpenAPI call
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>();

// Dependency Injection registration
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

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