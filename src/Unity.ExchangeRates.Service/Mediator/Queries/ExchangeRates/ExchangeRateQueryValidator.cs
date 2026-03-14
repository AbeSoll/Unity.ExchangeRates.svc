using FluentValidation;

namespace Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates
{
    public sealed class ExchangeRateQueryValidator : AbstractValidator<ExchangeRateQuery>
    {
        public ExchangeRateQueryValidator()
        {
            // Date is optional — when omitted, the handler resolves to the latest available date.
            // When provided, it must be in yyyy-MM-dd format.
            RuleFor(c => c.date)
                .Matches(@"^\d{4}-\d{2}-\d{2}$")
                .WithErrorCode("00400")
                .WithMessage("Date must be in yyyy-MM-dd format.")
                .When(c => !string.IsNullOrEmpty(c.date));
        }
    }
}
