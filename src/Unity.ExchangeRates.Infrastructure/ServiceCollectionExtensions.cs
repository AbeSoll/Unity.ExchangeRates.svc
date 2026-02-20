using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unity.ExchangeRates.Infrastructure.Data;
using Unity.ExchangeRates.Infrastructure.Interceptors;
using Unity.ExchangeRates.Infrastructure.Repositories;
using Unity.ExchangeRates.Repository;

namespace Unity.ExchangeRates.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterInfrastructureModule(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddContext(configuration)
                .AddPersistence();

            return services;
        }

        private static IServiceCollection AddContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddScoped<EntitySaveChangeInterceptor>();
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<EntitySaveChangeInterceptor>();
                options.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.CommandTimeout(600))
                       .AddInterceptors(interceptor);
            });

            return services;
        }

        private static IServiceCollection AddPersistence(this IServiceCollection services)
        {
            services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
            return services;
        }
    }
}
