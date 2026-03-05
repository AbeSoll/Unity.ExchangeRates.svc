using FluentValidation;
using Newtonsoft.Json.Linq;
using System.Net;
using Unity.ExchangeRates.Domain.Exceptions;

namespace Unity.ExchangeRates.Api.Middlewares
{
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlerMiddleware> _logger;

        public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
        {
            _next = next;
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

                switch (error)
                {
                    case ExchangeRatesDomainException:
                        // custom application error — expected domain validation, not a real error
                        _logger.LogWarning(error, "Domain exception: {Message}", error?.Message);
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        resultObject.message = new JValue(error?.Message);
                        break;
                    case ValidationException e:
                        // user input validation failure — expected, not a real error
                        _logger.LogWarning(error, "Validation exception: {Message}", error?.Message);
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        resultObject.message = e.Errors.Select(e => e.ErrorMessage).Distinct().FirstOrDefault();
                        break;
                    default:
                        // unhandled/unexpected error — never expose internal details to clients
                        _logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        resultObject.message = new JValue("An unexpected error occurred. Please try again later.");
                        break;
                }

                string result = resultObject.ToString();
                await response.WriteAsync(result);
            }
        }
    }
}
