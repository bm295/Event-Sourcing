using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

internal static class ConsumerEventGuard
{
    public static async Task<(string orderId, string partitionKey, SequenceGuardDecision decision)> ValidateAndLogAsync<TEvent>(
        ILogger logger,
        IConsumerSequenceGuardStore sequenceGuardStore,
        string consumerName,
        TEvent @event)
        where TEvent : IEventEnvelope
    {
        if (string.IsNullOrWhiteSpace(@event.OrderId)) throw new InvalidOperationException($"Missing OrderId on event type {@event.EventType}.");
        var decision = await sequenceGuardStore.CheckAndRecordAsync(consumerName, @event.OrderId, @event.SequenceNumber);
        var partitionKey = @event.GetPartitionKey();
        logger.LogInformation("Processing event {event_type} order={order_id} seq={seq} decision={decision}", @event.EventType, @event.OrderId, @event.SequenceNumber, decision);
        return (@event.OrderId, partitionKey, decision);
    }
}
