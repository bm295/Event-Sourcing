using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class ShippingOnPaymentAuthorizedHandler(
    IShippingPort shippingPort,
    IEventBus eventBus,
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public async Task HandleAsync(PaymentAuthorized @event)
    {
        if (!await deduplicationStore.TryMarkProcessedAsync(nameof(ShippingOnPaymentAuthorizedHandler), @event.EventId))
        {
            return;
        }

        shippingPort.Prepare(@event, $"{nameof(ShippingOnPaymentAuthorizedHandler)}:{@event.EventId}");

        if (!await deduplicationStore.TryMarkProcessedAsync(
                nameof(ShippingOnPaymentAuthorizedHandler),
                DeterministicGuid.FromSource(@event.EventId, nameof(ShipmentPrepared))))
        {
            return;
        }

        var metadata = EventMetadata.NewChild(nameof(ShipmentPrepared), @event);
        var shipmentPrepared = new ShipmentPrepared(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            @event.CustomerId,
            packageCount: 1);

        await eventBus.PublishAsync(shipmentPrepared);
    }
}

public sealed class NotifyOnPaymentAuthorizedHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public async Task HandleAsync(PaymentAuthorized @event)
    {
        if (!await deduplicationStore.TryMarkProcessedAsync(nameof(NotifyOnPaymentAuthorizedHandler), @event.EventId))
        {
            return;
        }

        notificationPort.Send(
            $"Payment authorized for order {@event.OrderId}.",
            $"{nameof(NotifyOnPaymentAuthorizedHandler)}:{@event.EventId}");
    }
}
