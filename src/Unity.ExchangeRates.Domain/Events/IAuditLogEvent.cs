namespace Unity.ExchangeRates.Domain.Events
{
    public interface IAuditLogEvent : IEvent
    {
        public string EventType { get; }
        public string ReferenceId { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
