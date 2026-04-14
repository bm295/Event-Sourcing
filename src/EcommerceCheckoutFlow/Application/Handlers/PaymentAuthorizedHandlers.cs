using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class ShippingOnPaymentAuthorizedHandler(IShippingPort shippingPort, IEventBus eventBus)
{
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public async Task HandleAsync(PaymentAuthorized @event)
    {
        shippingPort.Prepare(@event);

        var shipmentPrepared = new ShipmentPrepared(
            @event.OrderId,
            @event.CustomerId,
            packageCount: 1,
            DateTimeOffset.UtcNow);

        await eventBus.PublishAsync(shipmentPrepared);
    }
}

public sealed class NotifyOnPaymentAuthorizedHandler(INotificationPort notificationPort)
{
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public Task HandleAsync(PaymentAuthorized @event)
    {
        notificationPort.Send($"Payment authorized for order {@event.OrderId}.");
        return Task.CompletedTask;
    }
}
