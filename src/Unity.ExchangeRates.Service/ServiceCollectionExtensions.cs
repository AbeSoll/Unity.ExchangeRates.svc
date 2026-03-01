using Audit.Core.Providers;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Unity.ExchangeRates.Service.Behaviors;
using Unity.ExchangeRates.Service.Configurations;

namespace Unity.ExchangeRates.Service
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterServiceModule(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddService(configuration)
                .AddAppSettings(configuration);

            // Audit.NET file data provider
            Audit.Core.Configuration.DataProvider = new FileDataProvider(cfg => cfg.Directory("audit-logs"));

            // Required for AuditLogEventHandler to access HttpContext
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            return services;
        }

        public static IServiceCollection AddService(this IServiceCollection services, IConfiguration configuration)
        {
            ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
            ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            return services;
        }

        private static IServiceCollection AddAppSettings(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<BnmApiOptions>(configuration.GetSection("BnmApiSettings"));
            return services;
        }
    }
}
