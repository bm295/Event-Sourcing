using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Handlers;

public sealed class InventoryOnOrderPlacedHandler(
    IInventoryPort inventoryPort,
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public async Task HandleAsync(OrderPlaced @event)
    {
        const string consumerName = nameof(InventoryOnOrderPlacedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        inventoryPort.ReserveItems(@event, $"{consumerName}:{eventId}");
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
        const string consumerName = nameof(PaymentOnOrderPlacedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        try
        {
            paymentPort.Authorize(@event, $"{consumerName}:{eventId}");

            if (!await deduplicationStore.TryMarkProcessedAsync(
                    consumerName,
                    DeterministicGuid.FromSource(eventId, nameof(PaymentAuthorized))))
            {
                return;
            }

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
            if (!await deduplicationStore.TryMarkProcessedAsync(
                    consumerName,
                    DeterministicGuid.FromSource(eventId, nameof(PaymentFailed))))
            {
                return;
            }

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

internal static class DeterministicGuid
{
    public static Guid FromSource(Guid sourceEventId, string operation)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{sourceEventId:N}:{operation}");
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, guidBytes.Length);
        return new Guid(guidBytes);
    }
}

public sealed class AnalyticsOnOrderPlacedHandler(
    IAnalyticsPort analyticsPort,
    IMessageDeduplicationStore deduplicationStore)
{
    [CapSubscribe(EventTopics.OrderPlaced)]
    public async Task HandleAsync(OrderPlaced @event)
    {
        const string consumerName = nameof(AnalyticsOnOrderPlacedHandler);
        var eventId = @event.EventId;
        if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
        {
            return;
        }

        analyticsPort.TrackOrder(@event);
    }
}
