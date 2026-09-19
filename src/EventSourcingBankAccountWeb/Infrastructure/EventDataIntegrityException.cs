namespace EventSourcingBankAccountWeb.Infrastructure;

public sealed class EventDataIntegrityException(string eventId)
    : Exception($"The signature for event '{eventId}' is invalid. The event stream may have been altered.")
{
    public string EventId { get; } = eventId;
}
