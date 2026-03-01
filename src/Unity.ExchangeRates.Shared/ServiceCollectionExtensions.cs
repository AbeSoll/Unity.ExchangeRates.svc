using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Unity.ExchangeRates.Service.Configurations;
using Unity.ExchangeRates.Service.Services;
using Unity.ExchangeRates.Shared.Jobs;
using Unity.ExchangeRates.Shared.Services;

namespace Unity.ExchangeRates.Shared
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterSharedServiceModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IAuditLogEventDispatcher, AuditLogEventDispatcher>();

            services
                .AddHttpClients(configuration)
                .AddHangfireServices(configuration);

            return services;
        }

        private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient("BnmClient", (serviceProvider, client) =>
            {
                var settings = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<BnmApiOptions>>()
                    .Value;

                client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + '/');
                client.DefaultRequestHeaders.Add("Accept", settings.AcceptHeader);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddPolicyHandler(BuildRetryPolicy());

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

        private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5)
                });
        }
    }
}
