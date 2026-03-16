using Serilog;
using Serilog.Configuration;

namespace Unity.ExchangeRates.Api.Configurations.Logging
{
    public static class LoggingExtensions
    {
        public static LoggerConfiguration WithMethodName(
            this LoggerEnrichmentConfiguration enrich)
        {
            return enrich == null ? throw new ArgumentNullException(nameof(enrich)) : enrich.With<LogMethodNameEnricher>();
        }
    }
}