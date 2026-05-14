using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class CancelOrderOnPaymentFailedHandler(
    IEventBus eventBus,
    IMessageDeduplicationStore deduplicationStore,
    ILogger<CancelOrderOnPaymentFailedHandler> logger)
{
    // CAP subscriber is runtime-only, không dùng cho replay.
    [CapSubscribe(EventTopics.PaymentFailed)]
    public async Task HandleAsync(PaymentFailed @event)
    {
        var (_, _) = ConsumerEventGuard.ValidateAndLog(logger, @event);
        const string consumerName = nameof(CancelOrderOnPaymentFailedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        var metadata = EventMetadata.NewChild(nameof(OrderCancelled), @event);
        var orderCancelled = new OrderCancelled(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            @event.CustomerId,
            $"Payment failed: {@event.Reason}");

        await eventBus.PublishAsync(orderCancelled, @event.GetPartitionKey());
    }
}

public sealed class NotifyOnPaymentFailedHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore,
    ILogger<NotifyOnPaymentFailedHandler> logger)
{
    // CAP subscriber is runtime-only, không dùng cho replay.
    [CapSubscribe(EventTopics.PaymentFailed)]
    public async Task HandleAsync(PaymentFailed @event)
    {
        var (_, _) = ConsumerEventGuard.ValidateAndLog(logger, @event);
        const string consumerName = nameof(NotifyOnPaymentFailedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        notificationPort.Send(
            $"Payment failed for order {@event.OrderId}: {@event.Reason}",
            $"{consumerName}:{eventId}");
    }
}

public sealed class NotifyOnOrderCancelledHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore,
    ILogger<NotifyOnOrderCancelledHandler> logger)
{
    // CAP subscriber is runtime-only, không dùng cho replay.
    [CapSubscribe(EventTopics.OrderCancelled)]
    public async Task HandleAsync(OrderCancelled @event)
    {
        var (_, _) = ConsumerEventGuard.ValidateAndLog(logger, @event);
        const string consumerName = nameof(NotifyOnOrderCancelledHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        notificationPort.Send(
            $"Order {@event.OrderId} cancelled. Reason: {@event.Reason}",
            $"{consumerName}:{eventId}");
    }
}
