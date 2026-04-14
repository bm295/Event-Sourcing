using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.UseCases;

public sealed class CheckoutUseCase(IEventBus eventBus)
{
    public Task PlaceOrderAsync(
        string orderId,
        string customerId,
        IReadOnlyList<CartItem> items,
        CancellationToken cancellationToken = default)
    {
        var order = Order.Create(orderId, customerId, items);

        var orderPlaced = new OrderPlaced(
            order.OrderId,
            order.CustomerId,
            order.Items,
            order.TotalAmount,
            DateTimeOffset.UtcNow);

        return eventBus.PublishAsync(orderPlaced, cancellationToken);
    }
}
