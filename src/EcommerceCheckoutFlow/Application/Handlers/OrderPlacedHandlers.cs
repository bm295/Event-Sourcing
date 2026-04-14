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

public sealed class PaymentOnOrderPlacedHandler(IPaymentPort paymentPort, IEventBus eventBus)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public async Task HandleAsync(OrderPlaced @event)
    {
        paymentPort.Authorize(@event);

        var paymentAuthorized = new PaymentAuthorized(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount,
            DateTimeOffset.UtcNow);

        await eventBus.PublishAsync(paymentAuthorized);
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
