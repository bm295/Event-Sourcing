using System;
using System.Threading;
using System.Threading.Tasks;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Application.Handlers;

internal static class ConsumerEventGuard
{
    public static async Task<ConsumerEventValidationResult> ValidateAndLogAsync(
        ILogger logger,
        IConsumerSequenceGuardStore sequenceGuardStore,
        string consumerName,
        IEventEnvelope @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(sequenceGuardStore);
        ArgumentNullException.ThrowIfNull(@event);

        if (string.IsNullOrWhiteSpace(@event.OrderId))
        {
            throw new InvalidOperationException($"Missing OrderId on event type {@event.EventType}.");
        }

        var decision = await sequenceGuardStore.CheckAndRecordAsync(
            consumerName,
            @event.OrderId,
            @event.SequenceNumber,
            cancellationToken);

        var partitionKey = @event.GetPartitionKey();

        logger.LogInformation(
            "Processing event {event_type} order={order_id} seq={seq} decision={decision}",
            @event.EventType,
            @event.OrderId,
            @event.SequenceNumber,
            decision);

        return new ConsumerEventValidationResult(@event.OrderId, partitionKey, decision);
    }
}

internal sealed record ConsumerEventValidationResult(string OrderId, string PartitionKey, SequenceGuardDecision Decision);
