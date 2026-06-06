using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class InventoryOnOrderPlacedHandler(
    IInventoryPort inventoryPort,
    IMessageDeduplicationStore deduplicationStore,
    IConsumerSequenceGuardStore sequenceGuardStore,
    ILogger<InventoryOnOrderPlacedHandler> logger)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public async Task HandleAsync(OrderPlaced @event)
    {
        const string consumerName = nameof(InventoryOnOrderPlacedHandler);

        var validationResult = await OrderPlacedHandlersHelper.GuardAndDeduplicateAsync(
            logger,
            sequenceGuardStore,
            deduplicationStore,
            consumerName,
            @event);

        if (validationResult is null) return;
        inventoryPort.ReserveItems(@event, $"{consumerName}:{@event.EventId}");
    }
}

public sealed class PaymentOnOrderPlacedHandler(
    IPaymentPort paymentPort,
    IEventBus eventBus,
    IMessageDeduplicationStore deduplicationStore,
    IConsumerSequenceGuardStore sequenceGuardStore,
    IOrderEventSequenceAllocator sequenceAllocator,
    ILogger<PaymentOnOrderPlacedHandler> logger)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public async Task HandleAsync(OrderPlaced @event)
    {
        const string consumerName = nameof(PaymentOnOrderPlacedHandler);
        var validationResult = await OrderPlacedHandlersHelper.GuardAndDeduplicateAsync(
            logger,
            sequenceGuardStore,
            deduplicationStore,
            consumerName,
            @event);

        if (validationResult is null) return;
        var partitionKey = validationResult.PartitionKey;

        try
        {
            paymentPort.Authorize(@event, $"{consumerName}:{@event.EventId}");
            var seq = await sequenceAllocator.AllocateNextSequenceAsync(@event.OrderId);
            var metadata = EventMetadata.NewChild(nameof(PaymentAuthorized), @event, seq);
            var next = new PaymentAuthorized(metadata.EventId, metadata.OccurredAt, metadata.CorrelationId, metadata.CausationId, metadata.EventType, metadata.OrderId, metadata.SequenceNumber, @event.CustomerId, @event.TotalAmount);
            await eventBus.PublishAsync(next, partitionKey);
        }
        catch (Exception ex)
        {
            var seq = await sequenceAllocator.AllocateNextSequenceAsync(@event.OrderId);
            var metadata = EventMetadata.NewChild(nameof(PaymentFailed), @event, seq);
            var failed = new PaymentFailed(metadata.EventId, metadata.OccurredAt, metadata.CorrelationId, metadata.CausationId, metadata.EventType, metadata.OrderId, metadata.SequenceNumber, @event.CustomerId, @event.TotalAmount, ex.Message);
            await eventBus.PublishAsync(failed, partitionKey);
        }
    }
}

public sealed class AnalyticsOnOrderPlacedHandler(
    IAnalyticsPort analyticsPort,
    IMessageDeduplicationStore deduplicationStore,
    IConsumerSequenceGuardStore sequenceGuardStore,
    ILogger<AnalyticsOnOrderPlacedHandler> logger)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public async Task HandleAsync(OrderPlaced @event)
    {
        const string consumerName = nameof(AnalyticsOnOrderPlacedHandler);
        var validationResult = await OrderPlacedHandlersHelper.GuardAndDeduplicateAsync(
            logger,
            sequenceGuardStore,
            deduplicationStore,
            consumerName,
            @event);

        if (validationResult is null) return;
        analyticsPort.TrackOrder(@event);
    }
}

internal static class OrderPlacedHandlersHelper
{
    public static async Task<ConsumerEventValidationResult?> GuardAndDeduplicateAsync(
        ILogger logger,
        IConsumerSequenceGuardStore sequenceGuardStore,
        IMessageDeduplicationStore deduplicationStore,
        string consumerName,
        IEventEnvelope @event,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await ConsumerEventGuard.ValidateAndLogAsync(
            logger,
            sequenceGuardStore,
            consumerName,
            @event,
            cancellationToken);

        if (validationResult.Decision != SequenceGuardDecision.Accept)
        {
            return null;
        }

        if (!await deduplicationStore.TryMarkProcessedAsync(
                consumerName,
                @event.EventId,
                cancellationToken))
        {
            return null;
        }

        return validationResult;
    }
}
