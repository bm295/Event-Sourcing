using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class ShippingOnPaymentAuthorizedHandler(
    IShippingPort shippingPort,
    IEventBus eventBus,
    IMessageDeduplicationStore deduplicationStore,
    ILogger<ShippingOnPaymentAuthorizedHandler> logger)
{
    // CAP subscriber is runtime-only, không dùng cho replay.
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public async Task HandleAsync(PaymentAuthorized @event)
    {
        var (_, partitionKey) = ConsumerEventGuard.ValidateAndLog(logger, @event);
        const string consumerName = nameof(ShippingOnPaymentAuthorizedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        shippingPort.Prepare(@event, $"{consumerName}:{eventId}");

        if (!await deduplicationStore.TryMarkProcessedAsync(
                consumerName,
                DeterministicGuid.FromSource(eventId, nameof(ShipmentPrepared))))
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

        await eventBus.PublishAsync(shipmentPrepared, partitionKey);
    }
}

public sealed class NotifyOnPaymentAuthorizedHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore,
    ILogger<NotifyOnPaymentAuthorizedHandler> logger)
{
    // CAP subscriber is runtime-only, không dùng cho replay.
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public async Task HandleAsync(PaymentAuthorized @event)
    {
        var (_, _) = ConsumerEventGuard.ValidateAndLog(logger, @event);
        const string consumerName = nameof(NotifyOnPaymentAuthorizedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        notificationPort.Send(
            $"Payment authorized for order {@event.OrderId}.",
            $"{consumerName}:{eventId}");
    }
}
