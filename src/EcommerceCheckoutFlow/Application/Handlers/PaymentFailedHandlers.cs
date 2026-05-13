using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class CancelOrderOnPaymentFailedHandler(IEventBus eventBus)
{
    [CapSubscribe(EventTopics.PaymentFailed)]
    public async Task HandleAsync(PaymentFailed @event)
    {
        var orderCancelled = new OrderCancelled(
            @event.OrderId,
            @event.CustomerId,
            $"Payment failed: {@event.Reason}",
            DateTimeOffset.UtcNow);

        await eventBus.PublishAsync(orderCancelled);
    }
}

public sealed class NotifyOnPaymentFailedHandler(INotificationPort notificationPort)
{
    [CapSubscribe(EventTopics.PaymentFailed)]
    public Task HandleAsync(PaymentFailed @event)
    {
        notificationPort.Send($"Payment failed for order {@event.OrderId}: {@event.Reason}");
        return Task.CompletedTask;
    }
}

public sealed class NotifyOnOrderCancelledHandler(INotificationPort notificationPort)
{
    [CapSubscribe(EventTopics.OrderCancelled)]
    public Task HandleAsync(OrderCancelled @event)
    {
        notificationPort.Send($"Order {@event.OrderId} cancelled. Reason: {@event.Reason}");
        return Task.CompletedTask;
    }
}
