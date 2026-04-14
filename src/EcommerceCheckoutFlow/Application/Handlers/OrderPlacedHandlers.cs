using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class InventoryOnOrderPlacedHandler(IInventoryPort inventoryPort)
{
    public void Handle(OrderPlaced @event) => inventoryPort.ReserveItems(@event);
}

public sealed class PaymentOnOrderPlacedHandler(IPaymentPort paymentPort, IEventBus eventBus)
{
    public void Handle(OrderPlaced @event)
    {
        paymentPort.Authorize(@event);

        var paymentAuthorized = new PaymentAuthorized(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount,
            DateTimeOffset.UtcNow);

        eventBus.Publish(paymentAuthorized);
    }
}

public sealed class AnalyticsOnOrderPlacedHandler(IAnalyticsPort analyticsPort)
{
    public void Handle(OrderPlaced @event) => analyticsPort.TrackOrder(@event);
}
