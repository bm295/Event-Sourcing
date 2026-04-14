using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class NotifyOnShipmentPreparedHandler(INotificationPort notificationPort)
{
    [CapSubscribe(EventTopics.ShipmentPrepared)]
    public Task HandleAsync(ShipmentPrepared @event)
    {
        notificationPort.Send($"Shipment prepared for order {@event.OrderId}.");
        return Task.CompletedTask;
    }
}
