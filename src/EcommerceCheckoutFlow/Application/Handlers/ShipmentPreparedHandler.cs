using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class NotifyOnShipmentPreparedHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore,
    ILogger<NotifyOnShipmentPreparedHandler> logger)
{
    // CAP subscriber is runtime-only, không dùng cho replay.
    [CapSubscribe(EventTopics.ShipmentPrepared)]
    public async Task HandleAsync(ShipmentPrepared @event)
    {
        var (_, _) = ConsumerEventGuard.ValidateAndLog(logger, @event);
        const string consumerName = nameof(NotifyOnShipmentPreparedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        notificationPort.Send(
            $"Shipment prepared for order {@event.OrderId}.",
            $"{consumerName}:{eventId}");
    }
}
