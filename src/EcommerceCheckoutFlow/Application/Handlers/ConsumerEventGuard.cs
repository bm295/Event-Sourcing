using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

internal static class ConsumerEventGuard
{
    public static (string orderId, string partitionKey) ValidateAndLog<TEvent>(ILogger logger, TEvent @event)
        where TEvent : IEventEnvelope
    {
        if (string.IsNullOrWhiteSpace(@event.OrderId))
        {
            throw new InvalidOperationException($"Missing OrderId on event type {@event.EventType}.");
        }

        var partitionKey = @event.GetPartitionKey();
        logger.LogInformation(
            "Processing event {event_type} with order_id={order_id}, partition_key={partition_key}, event_id={event_id}, causation_id={causation_id}, correlation_id={correlation_id}",
            @event.EventType,
            @event.OrderId,
            partitionKey,
            @event.EventId,
            @event.CausationId,
            @event.CorrelationId);

        return (@event.OrderId, partitionKey);
    }
}
