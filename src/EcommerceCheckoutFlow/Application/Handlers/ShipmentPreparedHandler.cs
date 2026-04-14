using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class NotifyOnShipmentPreparedHandler(INotificationPort notificationPort)
{
    public void Handle(ShipmentPrepared @event) =>
        notificationPort.Send($"Shipment prepared for order {@event.OrderId}.");
}
