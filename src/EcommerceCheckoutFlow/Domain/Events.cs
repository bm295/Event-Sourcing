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
    string OrderId)
{
    public static EventMetadata NewRoot(string eventType, string orderId, DateTimeOffset? occurredAt = null)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: null,
            EventType: eventType,
            OrderId: orderId);

    public static EventMetadata NewChild(string eventType, IEventEnvelope cause, DateTimeOffset? occurredAt = null)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            CorrelationId: cause.CorrelationId,
            CausationId: cause.EventId,
            EventType: eventType,
            OrderId: cause.OrderId);
}

public sealed record OrderPlaced(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
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
    string CustomerId,
    decimal Amount) : IDomainEvent, IEventEnvelope;

public sealed record PaymentFailed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
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
    string CustomerId,
    string Reason) : IDomainEvent, IEventEnvelope;

public sealed record ShipmentPrepared(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid? CausationId,
    string EventType,
    string OrderId,
    string CustomerId,
    int PackageCount) : IDomainEvent, IEventEnvelope;
