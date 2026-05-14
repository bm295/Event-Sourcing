using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class InventoryOnOrderPlacedHandler(IInventoryPort inventoryPort)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public Task HandleAsync(OrderPlaced @event)
    {
        inventoryPort.ReserveItems(@event);
        return Task.CompletedTask;
    }
}

public sealed class PaymentOnOrderPlacedHandler(
    IPaymentPort paymentPort,
    IEventBus eventBus,
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public async Task HandleAsync(OrderPlaced @event)
    {
        if (!await deduplicationStore.TryMarkProcessedAsync(nameof(PaymentOnOrderPlacedHandler), @event.EventId))
        {
            return;
        }

        try
        {
            paymentPort.Authorize(@event);

            var metadata = EventMetadata.NewChild(nameof(PaymentAuthorized), @event);
            var paymentAuthorized = new PaymentAuthorized(
                metadata.EventId,
                metadata.OccurredAt,
                metadata.CorrelationId,
                metadata.CausationId,
                metadata.EventType,
                metadata.OrderId,
                @event.CustomerId,
                @event.TotalAmount);

            await eventBus.PublishAsync(paymentAuthorized);
        }
        catch (Exception ex)
        {
            var metadata = EventMetadata.NewChild(nameof(PaymentFailed), @event);
            var paymentFailed = new PaymentFailed(
                metadata.EventId,
                metadata.OccurredAt,
                metadata.CorrelationId,
                metadata.CausationId,
                metadata.EventType,
                metadata.OrderId,
                @event.CustomerId,
                @event.TotalAmount,
                ex.Message);

            await eventBus.PublishAsync(paymentFailed);
        }
    }
}

public sealed class AnalyticsOnOrderPlacedHandler(IAnalyticsPort analyticsPort)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public Task HandleAsync(OrderPlaced @event)
    {
        analyticsPort.TrackOrder(@event);
        return Task.CompletedTask;
    }
}
