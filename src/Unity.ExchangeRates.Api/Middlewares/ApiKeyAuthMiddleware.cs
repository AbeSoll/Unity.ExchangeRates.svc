using System.Net;
using System.Text.Json;

namespace Unity.ExchangeRates.Api.Middlewares
{
    public class ApiKeyAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyAuthMiddleware> _logger;
        private const string ApiKeyHeaderName = "X-Api-Key";

        public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

            // Skip authentication for Swagger and Hangfire (dev tools)
            if (path.StartsWith("/swagger") || path.StartsWith("/hangfire"))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                _logger.LogWarning("API Key missing from request. Path={Path}, IP={IP}",
                    context.Request.Path, context.Connection.RemoteIpAddress);

                await WriteUnauthorizedResponse(context, "API Key is required. Provide it via the X-Api-Key header.");
                return;
            }

            var configuredApiKey = _configuration["ApiSecurity:ApiKey"];
            if (string.IsNullOrEmpty(configuredApiKey) || !string.Equals(extractedApiKey, configuredApiKey))
            {
                _logger.LogWarning("Invalid API Key provided. Path={Path}, IP={IP}",
                    context.Request.Path, context.Connection.RemoteIpAddress);

                await WriteUnauthorizedResponse(context, "Invalid API Key.");
                return;
            }

            await _next(context);
        }

        private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = "Failed",
                errorCode = "00401",
                errorMsg = message,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz",
                    System.Globalization.CultureInfo.InvariantCulture)
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
