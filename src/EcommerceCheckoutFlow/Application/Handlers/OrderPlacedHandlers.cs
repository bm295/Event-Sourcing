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
    ILogger<InventoryOnOrderPlacedHandler> logger) : ICapSubscribe
{
    [CapSubscribe(EventTopics.OrderPlaced, Group = nameof(InventoryOnOrderPlacedHandler))]
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
    IPaymentProcessingStore processingStore,
    ILogger<PaymentOnOrderPlacedHandler> logger) : ICapSubscribe
{
    [CapSubscribe(EventTopics.OrderPlaced, Group = nameof(PaymentOnOrderPlacedHandler))]
    public async Task HandleAsync(OrderPlaced @event)
    {
        if (@event.EventId == Guid.Empty || string.IsNullOrWhiteSpace(@event.OrderId) || @event.SequenceNumber < 1)
            throw new InvalidOperationException("Invalid payment input event metadata.");

        var state = await processingStore.FindAsync(@event.EventId);
        if (state?.Completed == true) return;

        if (state is null)
        {
            string? declineReason = null;
            try
            {
                paymentPort.Authorize(@event, $"{nameof(PaymentOnOrderPlacedHandler)}:{@event.EventId}");
            }
            catch (PaymentDeclinedException ex)
            {
                declineReason = ex.Message;
            }

            // Like ENode's persisted event stream, reuse the saved outcome on
            // redelivery. Never rerun a successful authorization to republish it.
            state = await processingStore.SaveOutcomeAsync(@event, declineReason);
        }

        // Publish errors are infrastructure failures, never payment rejections.
        // An ambiguous publish may be retried; the immutable EventId permits
        // downstream deduplication (delivery remains at least once).
        switch (state.Outcome)
        {
            case PaymentAuthorized authorized:
                await eventBus.PublishAsync(authorized, authorized.GetPartitionKey());
                break;
            case PaymentFailed failed:
                await eventBus.PublishAsync(failed, failed.GetPartitionKey());
                break;
            default:
                throw new InvalidOperationException("Unsupported payment outcome.");
        }
        await processingStore.CompleteAsync(@event.EventId);
        logger.LogInformation("Payment consumer completed input {EventId}", @event.EventId);
    }
}

public sealed class AnalyticsOnOrderPlacedHandler(
    IAnalyticsPort analyticsPort,
    IMessageDeduplicationStore deduplicationStore,
    IConsumerSequenceGuardStore sequenceGuardStore,
    ILogger<AnalyticsOnOrderPlacedHandler> logger) : ICapSubscribe
{
    [CapSubscribe(EventTopics.OrderPlaced, Group = nameof(AnalyticsOnOrderPlacedHandler))]
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
