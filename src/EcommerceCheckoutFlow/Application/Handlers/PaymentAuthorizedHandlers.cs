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
    IConsumerSequenceGuardStore sequenceGuardStore,
    IOrderEventSequenceAllocator sequenceAllocator,
    ILogger<ShippingOnPaymentAuthorizedHandler> logger)
{
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public async Task HandleAsync(PaymentAuthorized @event)
    {
        const string consumerName = nameof(ShippingOnPaymentAuthorizedHandler);
        var (_, partitionKey, decision) = await ConsumerEventGuard.ValidateAndLogAsync(logger, sequenceGuardStore, consumerName, @event);
        if (decision != SequenceGuardDecision.Accept) return;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, @event.EventId)) return;
        shippingPort.Prepare(@event, $"{consumerName}:{@event.EventId}");
        var seq = await sequenceAllocator.AllocateNextSequenceAsync(@event.OrderId);
        var metadata = EventMetadata.NewChild(nameof(ShipmentPrepared), @event, seq);
        var next = new ShipmentPrepared(metadata.EventId, metadata.OccurredAt, metadata.CorrelationId, metadata.CausationId, metadata.EventType, metadata.OrderId, metadata.SequenceNumber, @event.CustomerId, 1);
        await eventBus.PublishAsync(next, partitionKey);
    }
}

public sealed class NotifyOnPaymentAuthorizedHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore,
    IConsumerSequenceGuardStore sequenceGuardStore,
    ILogger<NotifyOnPaymentAuthorizedHandler> logger)
{
    [CapSubscribe(EventTopics.PaymentAuthorized)]
    public async Task HandleAsync(PaymentAuthorized @event)
    {
        const string consumerName = nameof(NotifyOnPaymentAuthorizedHandler);
        var (_, _, decision) = await ConsumerEventGuard.ValidateAndLogAsync(logger, sequenceGuardStore, consumerName, @event);
        if (decision != SequenceGuardDecision.Accept) return;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, @event.EventId)) return;
        notificationPort.Send($"Payment authorized for order {@event.OrderId}.", $"{consumerName}:{@event.EventId}");
    }
}
