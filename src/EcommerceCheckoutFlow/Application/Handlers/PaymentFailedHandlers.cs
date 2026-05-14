using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class CancelOrderOnPaymentFailedHandler(
    IEventBus eventBus,
    IMessageDeduplicationStore deduplicationStore,
    IConsumerSequenceGuardStore sequenceGuardStore,
    IOrderEventSequenceAllocator sequenceAllocator,
    ILogger<CancelOrderOnPaymentFailedHandler> logger)
{
    [CapSubscribe(EventTopics.PaymentFailed)]
    public async Task HandleAsync(PaymentFailed @event)
    {
        const string consumerName = nameof(CancelOrderOnPaymentFailedHandler);
        var (_, _, decision) = await ConsumerEventGuard.ValidateAndLogAsync(logger, sequenceGuardStore, consumerName, @event);
        if (decision != SequenceGuardDecision.Accept) return;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, @event.EventId)) return;
        var seq = await sequenceAllocator.AllocateNextSequenceAsync(@event.OrderId);
        var metadata = EventMetadata.NewChild(nameof(OrderCancelled), @event, seq);
        var next = new OrderCancelled(metadata.EventId, metadata.OccurredAt, metadata.CorrelationId, metadata.CausationId, metadata.EventType, metadata.OrderId, metadata.SequenceNumber, @event.CustomerId, $"Payment failed: {@event.Reason}");
        await eventBus.PublishAsync(next, @event.GetPartitionKey());
    }
}
