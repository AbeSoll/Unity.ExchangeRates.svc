using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Unity.ExchangeRates.Api.Configurations.Swagger
{
    public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider _provider;

        public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        {
            _provider = provider;
        }

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
        }
    }
}
