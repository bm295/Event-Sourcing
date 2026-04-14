using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class ShippingOnPaymentAuthorizedHandler(IShippingPort shippingPort, IEventBus eventBus)
{
    public void Handle(PaymentAuthorized @event)
    {
        shippingPort.Prepare(@event);

        var shipmentPrepared = new ShipmentPrepared(
            @event.OrderId,
            @event.CustomerId,
            packageCount: 1,
            DateTimeOffset.UtcNow);

        eventBus.Publish(shipmentPrepared);
    }
}

public sealed class NotifyOnPaymentAuthorizedHandler(INotificationPort notificationPort)
{
    public void Handle(PaymentAuthorized @event) =>
        notificationPort.Send($"Payment authorized for order {@event.OrderId}.");
}
