using Audit.WebApi;

namespace Unity.ExchangeRates.Api.Configurations
{
    public static class AuditConfigurationBuilderExtensions
    {
        public static IApplicationBuilder UseAuditLog(this WebApplication builder)
        {
            builder.UseAuditMiddleware(_ => _
            .FilterByRequest(rq => !rq.Path.Value.EndsWith("favicon.ico"))
            .WithEventType("{verb}:{url}")
            .IncludeHeaders()
            .IncludeResponseHeaders()
            .IncludeRequestBody()
            .IncludeResponseBody(ctx => ctx.Response.StatusCode != 200));

            //https://github.com/thepirat000/Audit.NET/blob/master/src/Audit.WebApi/README.md#note
            builder.Use(async (context, next) => {
                context.Request.EnableBuffering();
                await next();
            });

            return builder;
        }
    }
}
