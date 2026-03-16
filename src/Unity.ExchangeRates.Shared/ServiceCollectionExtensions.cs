using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using Unity.ExchangeRates.Service.Configurations;
using Unity.ExchangeRates.Shared.Jobs;

namespace Unity.ExchangeRates.Shared
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterSharedServiceModule(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddHttpClients(configuration)
                .AddHangfireServices(configuration);

            return services;
        }

        private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration _)
        {
            services.AddHttpClient("BnmClient", (serviceProvider, client) =>
            {
                var settings = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<BnmApiOptions>>()
                    .Value;

                client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + '/');
                client.DefaultRequestHeaders.Add("Accept", settings.AcceptHeader);

                // Let Polly manage per-attempt timeouts; disable HttpClient's global timeout
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            // Outer: retry policy (wraps timeout — retries on per-attempt timeout)
            .AddPolicyHandler(BuildRetryPolicy())
            // Inner: per-attempt timeout (each individual request gets 10s)
            .AddPolicyHandler(BuildTimeoutPolicy());

            return services;
        }

        private static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHangfire(config =>
                config.UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection")));
            services.AddHangfireServer();
            services.AddScoped<IExchangeRateSyncJob, ExchangeRateSyncJob>();
            return services;
        }

        /// <summary>
        /// Retries on transient HTTP errors (5xx, 408) and per-attempt timeouts.
        /// 3 retries with exponential backoff: 1s → 2s → 5s.
        /// </summary>
        private static AsyncRetryPolicy<HttpResponseMessage> BuildRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                [
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5)
                ]);
        }

        /// <summary>
        /// Each individual HTTP attempt gets 10 seconds before Polly cancels it.
        /// </summary>
        private static AsyncTimeoutPolicy<HttpResponseMessage> BuildTimeoutPolicy()
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));
        }
    }
}
