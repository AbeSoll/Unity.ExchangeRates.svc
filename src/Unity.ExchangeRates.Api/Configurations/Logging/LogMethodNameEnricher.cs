using Serilog.Core;
using Serilog.Events;
using System.Runtime.CompilerServices;

namespace Unity.ExchangeRates.Api.Configurations.Logging
{
    public class LogMethodNameEnricher : ILogEventEnricher
    {
        public const string PropertyName = "MethodName";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            LogEventPropertyValue value;
            if (!logEvent.Properties.TryGetValue("SourceContext", out value) || value is not ScalarValue scalarValue)
                return;

            var SourceContextStr = (string)scalarValue.Value;

            if (!logEvent.Properties.ContainsKey(PropertyName))
            {
                var stackFrame = new List<dynamic>();

                string caller = String.Empty;
                foreach (var frame in stackFrame)
                {
                    var method = frame.GetMethod();
                    if (method!.DeclaringType != null)
                    {
                        if (method!.DeclaringType.FullName == SourceContextStr)
                        {
                            caller = $".{method.Name}";
                            break;
                        }

                        if (method!.DeclaringType.FullName.Contains(SourceContextStr))
                        {
                            var dtName = method!.DeclaringType!.Name;
                            caller = dtName.Contains(">") ? $".{dtName.Substring(1, dtName.LastIndexOf(">") - 1)}" : dtName;
                        }
                    }
                }

                logEvent.AddPropertyIfAbsent(new LogEventProperty("MethodName", new ScalarValue(caller)));
            }
        }
    }
}