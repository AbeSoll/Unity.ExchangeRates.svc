using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unity.ExchangeRates.Api.Configurations.Logging
{
    public class LogMethodNameEnricher : ILogEventEnricher
    {
        public const string PropertyName = "MethodName";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.ContainsKey(PropertyName))
                return;

            if (!logEvent.Properties.TryGetValue("SourceContext", out var value) || value is not ScalarValue scalarValue)
                return;

            var sourceContext = scalarValue.Value as string;
            if (string.IsNullOrEmpty(sourceContext))
                return;

            var caller = string.Empty;
            var stackTrace = new StackTrace(fNeedFileInfo: false);

            foreach (var frame in stackTrace.GetFrames())
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType == null) continue;

                var fullName = method.DeclaringType.FullName ?? string.Empty;

                if (fullName == sourceContext)
                {
                    caller = $".{method.Name}";
                    break;
                }

                if (fullName.Contains(sourceContext))
                {
                    var dtName = method.DeclaringType.Name;
                    caller = dtName.Contains('>')
                        ? $".{dtName[1..dtName.LastIndexOf('>')]}"
                        : $".{dtName}";
                    break;
                }
            }

            logEvent.AddPropertyIfAbsent(new LogEventProperty(PropertyName, new ScalarValue(caller)));
        }
    }
}