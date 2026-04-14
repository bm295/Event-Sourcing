namespace EcommerceCheckoutFlow.Domain;

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
}
