using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace Unity.ExchangeRates.Api.Configurations.Swagger
{
    public class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider _provider = provider;

        public void Configure(SwaggerGenOptions options)
        {
            foreach (var description in _provider.ApiVersionDescriptions)
            {
                var apiInfo = new OpenApiInfo
                {
                    Title = "Unity Exchange Rates API",
                    Version = $"{description.ApiVersion}"
                };
                if (description.IsDeprecated)
                {
                    apiInfo.Description += " This API version has been deprecated.";
                }
                options.SwaggerDoc(description.GroupName, apiInfo);
            }

            // Include XML comments for endpoint descriptions and response documentation
            var xmlFilename = $"{Assembly.GetEntryAssembly()!.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        }
    }
}
