namespace Unity.ExchangeRates.Domain.Events
{
    public class AuditLogEvent : IAuditLogEvent
    {
        public string EventType => this.Data.GetType().Name;
        public string ReferenceId { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
