namespace EcommerceCheckoutFlow.Domain;

public enum OrderStatus
{
    Placed,
    PaymentAuthorized,
    PaymentFailed,
    Cancelled,
    ShipmentPrepared
}

public sealed record Order(string OrderId, string CustomerId, IReadOnlyList<CartItem> Items, DateTimeOffset CreatedAt)
{
    public decimal TotalAmount => Items.Sum(item => item.LineTotal);

    public static Order Create(string orderId, string customerId, IReadOnlyList<CartItem> items)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Order requires at least one item.");
        }

        return new Order(orderId, customerId, items, DateTimeOffset.UtcNow);
    }

    public static OrderStatus DetermineStatus(IEnumerable<IDomainEvent> events)
    {
        var status = OrderStatus.Placed;

        foreach (var @event in events)
        {
            status = @event switch
            {
                OrderPlaced => OrderStatus.Placed,
                PaymentAuthorized => OrderStatus.PaymentAuthorized,
                PaymentFailed => OrderStatus.PaymentFailed,
                OrderCancelled => OrderStatus.Cancelled,
                ShipmentPrepared => OrderStatus.ShipmentPrepared,
                _ => status
            };
        }

        return status;
    }
}
