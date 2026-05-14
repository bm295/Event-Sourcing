using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class NotifyOnShipmentPreparedHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore,
    IConsumerSequenceGuardStore sequenceGuardStore,
    ILogger<NotifyOnShipmentPreparedHandler> logger)
{
    [CapSubscribe(EventTopics.ShipmentPrepared)]
    public async Task HandleAsync(ShipmentPrepared @event)
    {
        const string consumerName = nameof(NotifyOnShipmentPreparedHandler);
        var (_, _, decision) = await ConsumerEventGuard.ValidateAndLogAsync(logger, sequenceGuardStore, consumerName, @event);
        if (decision != SequenceGuardDecision.Accept) return;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, @event.EventId)) return;
        notificationPort.Send($"Shipment prepared for order {@event.OrderId}.", $"{consumerName}:{@event.EventId}");
    }
}
