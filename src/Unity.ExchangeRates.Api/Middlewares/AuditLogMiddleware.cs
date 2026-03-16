using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Infrastructure.Data;

namespace Unity.ExchangeRates.Api.Middlewares
{
    public class AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger, IServiceScopeFactory scopeFactory)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<AuditLogMiddleware> _logger = logger;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

        // Endpoints to skip audit logging
        private static readonly string[] SkipPaths = ["/swagger", "/hangfire", "/favicon.ico", "/health"];

        // Headers worth capturing (not all — only useful ones)
        private static readonly string[] CapturedHeaders = ["Content-Type", "Accept", "User-Agent", "X-Forwarded-For", "Authorization"];

        // Sensitive fields to redact from request body
        private static readonly string[] SensitiveFields = ["password", "token", "secret", "apiKey", "authorization"];

        private const int MaxBodyLength = 4096; // 4KB max body capture

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

            // Skip dev tool endpoints
            if (SkipPaths.Any(skip => path.StartsWith(skip)))
            {
                await _next(context);
                return;
            }

            // Enable request body buffering so it can be read multiple times
            context.Request.EnableBuffering();

            // Capture request data BEFORE processing
            var httpMethod = context.Request.Method;
            var endpoint = context.Request.Path.Value ?? string.Empty;
            var queryString = context.Request.QueryString.HasValue? SanitizeQueryString(context.Request.QueryString.Value!): null;
            var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

            // Capture selected request headers
            var requestHeaders = CaptureHeaders(context.Request.Headers);

            // Capture request body
            var requestBody = await CaptureRequestBodyAsync(context.Request);

            // Replace response body stream to capture response
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // Start timing
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                // Capture response body (only for non-200 responses)
                string? responseBody = null;
                if (context.Response.StatusCode != 200)
                {
                    responseBody = await CaptureResponseBodyAsync(responseBodyStream);
                }

                // Copy response body back to original stream
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalBodyStream);
                context.Response.Body = originalBodyStream;

                // Save audit log to database
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var auditLog = new AuditLog
                    {
                        TraceId = traceId,
                        HttpMethod = httpMethod,
                        Endpoint = endpoint,
                        QueryString = queryString,
                        RequestHeaders = requestHeaders,
                        RequestBody = requestBody,
                        ResponseStatusCode = context.Response.StatusCode,
                        ResponseBody = responseBody,
                        ClientIpAddress = clientIp,
                        //UserAgent = TruncateString(userAgent, 500),
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        CreatedOn = DateTime.Now
                    };

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            dbContext.AuditLogs.Add(auditLog);
                            await dbContext.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "AuditLogMiddleware: Failed to save audit log for {Method} {Endpoint}", httpMethod, endpoint);
                        }
                    });

                    _logger.LogDebug("AuditLogMiddleware: Saved audit log for {Method} {Endpoint} → {StatusCode} ({DurationMs}ms)",
                        httpMethod, endpoint, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    // Never let audit logging crash the request
                    _logger.LogError(ex, "AuditLogMiddleware: Failed to save audit log for {Method} {Endpoint}", httpMethod, endpoint);
                }
            }
        }

        private static string? CaptureHeaders(IHeaderDictionary headers)
        {
            var selectedHeaders = new Dictionary<string, string>();
            foreach (var headerName in CapturedHeaders)
            {
                if (headers.TryGetValue(headerName, out var value))
                {
                    // Mask Authorization header — only show scheme (e.g. "Bearer ***")
                    if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && value.ToString().Length > 10)
                    {
                        var scheme = value.ToString().Split(' ')[0];
                        selectedHeaders[headerName] = $"{scheme} ***";
                    }
                    else
                    {
                        selectedHeaders[headerName] = value.ToString();
                    }
                }
            }
            return selectedHeaders.Count > 0 ? JsonSerializer.Serialize(selectedHeaders) : null;
        }

        private static async Task<string?> CaptureRequestBodyAsync(HttpRequest request)
        {
            if (request.ContentLength == null || request.ContentLength == 0)
                return null;

            request.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin); // Reset for controller to read

            return SanitizeBody(TruncateString(body, MaxBodyLength));
        }

        private static async Task<string?> CaptureResponseBodyAsync(MemoryStream responseBodyStream)
        {
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(responseBodyStream, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            return TruncateString(body, MaxBodyLength);
        }

        private static string? TruncateString(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength] + "...[truncated]";
        }

        private static string? SanitizeBody(string? body)
        {
            if (string.IsNullOrEmpty(body)) return body;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object) return body;

                var dict = new Dictionary<string, object?>();
                foreach (var prop in root.EnumerateObject())
                {
                    if (SensitiveFields.Any(f => prop.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
                        dict[prop.Name] = "***REDACTED***";
                    else
                        dict[prop.Name] = prop.Value.ToString();
                }
                return JsonSerializer.Serialize(dict);
            }
            catch
            {
                return body; // Not JSON — return as-is
            }
        }

        private static string SanitizeQueryString(string queryString)
        {
            var sanitized = new List<string>();
            var pairs = queryString.TrimStart('?').Split('&');

            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && SensitiveFields.Any(f => parts[0].Contains(f, StringComparison.OrdinalIgnoreCase)))
                    sanitized.Add($"{parts[0]}=***REDACTED***");
                else
                    sanitized.Add(pair);
            }

            return "?" + string.Join("&", sanitized);
        }
    }
}
