using FluentResults;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.Service.Common.Errors;

namespace Unity.ExchangeRates.Service.Behaviors
{
    public class RequestValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators, ILoggerFactory logger) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : ResultBase<TResponse>, new()
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators = validators;
        private readonly ILogger<RequestValidationBehavior<TRequest, TResponse>> _logger = logger.CreateLogger<RequestValidationBehavior<TRequest, TResponse>>();

        public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var context = new ValidationContext<TRequest>(message);
            var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Count != 0)
            {
                var errors = failures.Select(f => new ValidationError()
                {
                    ErrorCode = f.ErrorCode,
                    ErrorMsg = f.ErrorMessage
                });

                _logger.LogWarning("Validation Error: {@errors}", errors);
                return new TResponse().WithErrors(errors);
            }

            return await next(message, cancellationToken);
        }
    }
}
