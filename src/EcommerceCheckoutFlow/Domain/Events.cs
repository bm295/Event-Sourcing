namespace EcommerceCheckoutFlow.Domain;

public interface IDomainEvent;

public sealed record OrderPlaced(
    string OrderId,
    string CustomerId,
    IReadOnlyList<CartItem> Items,
    decimal TotalAmount,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentAuthorized(
    string OrderId,
    string CustomerId,
    decimal Amount,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ShipmentPrepared(
    string OrderId,
    string CustomerId,
    int PackageCount,
    DateTimeOffset OccurredAt) : IDomainEvent;
