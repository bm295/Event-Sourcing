namespace EcommerceCheckoutFlow.Domain;

public interface IDomainEvent;

public interface IEventEnvelope
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    string CorrelationId { get; }
    Guid? CausationId { get; }
    string EventType { get; }
    string OrderId { get; }
    long SequenceNumber { get; }
}

public static class EventEnvelopeExtensions
{
    public static string GetPartitionKey(this IEventEnvelope @event) => @event.OrderId;
}

public sealed record EventMetadata(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
    long SequenceNumber)
{
    public static EventMetadata NewRoot(string eventType, string orderId, long sequenceNumber, DateTimeOffset? occurredAt = null)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: null,
            EventType: eventType,
            OrderId: orderId,
            SequenceNumber: sequenceNumber);

    public static EventMetadata NewChild(string eventType, IEventEnvelope cause, long sequenceNumber, DateTimeOffset? occurredAt = null)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            CorrelationId: cause.CorrelationId,
            CausationId: cause.EventId,
            EventType: eventType,
            OrderId: cause.OrderId,
            SequenceNumber: sequenceNumber);
}

public sealed record OrderPlaced(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
    long SequenceNumber,
    string CustomerId,
    IReadOnlyList<CartItem> Items,
    decimal TotalAmount) : IDomainEvent, IEventEnvelope;

public sealed record PaymentAuthorized(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
    long SequenceNumber,
    string CustomerId,
    decimal Amount) : IDomainEvent, IEventEnvelope;

public sealed record PaymentFailed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
    long SequenceNumber,
    string CustomerId,
    decimal Amount,
    string Reason) : IDomainEvent, IEventEnvelope;

public sealed record OrderCancelled(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
    long SequenceNumber,
    string CustomerId,
    string Reason) : IDomainEvent, IEventEnvelope;

public sealed record ShipmentPrepared(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
    long SequenceNumber,
    string CustomerId,
    int PackageCount) : IDomainEvent, IEventEnvelope;
