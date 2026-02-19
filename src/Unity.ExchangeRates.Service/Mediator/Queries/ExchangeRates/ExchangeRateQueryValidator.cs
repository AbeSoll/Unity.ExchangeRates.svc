using FluentValidation;

namespace Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates
{
    public sealed class ExchangeRateQueryValidator : AbstractValidator<ExchangeRateQuery>
    {
        public ExchangeRateQueryValidator()
        {
            RuleFor(c => c.currency)
                .NotEmpty()
                .WithErrorCode("00400")
                .WithMessage("Currency is required.");

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
