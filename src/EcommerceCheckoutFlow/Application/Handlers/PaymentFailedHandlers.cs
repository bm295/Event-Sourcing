using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class CancelOrderOnPaymentFailedHandler(
    IEventBus eventBus,
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.PaymentFailed)]
    public async Task HandleAsync(PaymentFailed @event)
    {
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

        await eventBus.PublishAsync(orderCancelled);
    }
}

public sealed class NotifyOnPaymentFailedHandler(
    INotificationPort notificationPort,
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.PaymentFailed)]
    public async Task HandleAsync(PaymentFailed @event)
    {
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
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.OrderCancelled)]
    public async Task HandleAsync(OrderCancelled @event)
    {
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
