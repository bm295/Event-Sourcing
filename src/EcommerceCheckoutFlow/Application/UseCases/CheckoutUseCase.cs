using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.UseCases;

public sealed class CheckoutUseCase(IEventBus eventBus)
{
    public void PlaceOrder(string orderId, string customerId, IReadOnlyList<CartItem> items)
    {
        var order = Order.Create(orderId, customerId, items);

        var orderPlaced = new OrderPlaced(
            order.OrderId,
            order.CustomerId,
            order.Items,
            order.TotalAmount,
            DateTimeOffset.UtcNow);

        eventBus.Publish(orderPlaced);
    }
}
