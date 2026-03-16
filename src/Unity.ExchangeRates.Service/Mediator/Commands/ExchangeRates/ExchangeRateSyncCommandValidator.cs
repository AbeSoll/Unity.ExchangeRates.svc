using FluentValidation;

namespace Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates
{
    public sealed class ExchangeRateSyncCommandValidator : AbstractValidator<ExchangeRateSyncCommand>
    {
        private static readonly HashSet<string> ValidSessions = ["0900", "1130", "1200", "1700"];

        public ExchangeRateSyncCommandValidator()
        {
            RuleFor(c => c.Date)
                .NotEmpty()
                .WithErrorCode("00400")
                .WithMessage("Date is required.")
                .Matches(@"^\d{4}-\d{2}-\d{2}$")
                .WithErrorCode("00400")
                .WithMessage("Date must be in yyyy-MM-dd format.");

            RuleFor(c => c.Session)
                .Must(s => ValidSessions.Contains(s!))
                .When(c => !string.IsNullOrEmpty(c.Session))
                .WithErrorCode("00400")
                .WithMessage("Session must be one of: 0900, 1130, 1200, 1700.");
        }
    }
}
