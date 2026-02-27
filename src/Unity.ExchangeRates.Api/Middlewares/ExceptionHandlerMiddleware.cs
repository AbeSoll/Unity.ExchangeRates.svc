using FluentValidation;
using Newtonsoft.Json.Linq;
using System.Net;
using Unity.ExchangeRates.Domain.Exceptions;

namespace Unity.ExchangeRates.Api.Middlewares
{
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ExceptionHandlerMiddleware> _logger;

        public ExceptionHandlerMiddleware(RequestDelegate next, IWebHostEnvironment env, ILogger<ExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _env = env;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception error)
            {
                var response = context.Response;
                response.ContentType = "application/json";
                dynamic resultObject = new JObject();
                resultObject.message = new JValue(error?.Message);

                switch (error)
                {
                    case ExchangeRatesDomainException:
                        // custom application error — expected domain validation, not a real error
                        _logger.LogWarning(error, "Domain exception: {Message}", error?.Message);
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        break;
                    case ValidationException e:
                        // user input validation failure — expected, not a real error
                        _logger.LogWarning(error, "Validation exception: {Message}", error?.Message);
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        resultObject.message = e.Errors.Select(e => e.ErrorMessage).Distinct().FirstOrDefault();
                        break;
                    default:
                        // unhandled/unexpected error
                        _logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        break;
                }

                string result = resultObject.ToString();
                await response.WriteAsync(result);
            }
        }
    }
}
