using FluentValidation;

namespace Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates
{
    public sealed class ExchangeRateSyncCommandValidator : AbstractValidator<ExchangeRateSyncCommand>
    {
        public ExchangeRateSyncCommandValidator()
        {
            RuleFor(c => c.date)
                .NotEmpty()
                .WithErrorCode("00400")
                .WithMessage("Date is required.")
                .Matches(@"^\d{4}-\d{2}-\d{2}$")
                .WithErrorCode("00400")
                .WithMessage("Date must be in yyyy-MM-dd format.");
        }
    }
}
