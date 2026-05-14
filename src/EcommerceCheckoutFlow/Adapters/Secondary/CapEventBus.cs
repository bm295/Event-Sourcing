using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Secondary;

public sealed class CapEventBus(ICapPublisher capPublisher) : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, string partitionKey, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var topic = ResolveTopic<TEvent>();
        var headers = new Dictionary<string, string?>
        {
            ["partitionKey"] = partitionKey
        };

        return capPublisher.PublishAsync(topic, @event, headers, cancellationToken);
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        if (@event is not IEventEnvelope envelope)
        {
            throw new InvalidOperationException($"Event {typeof(TEvent).Name} must implement {nameof(IEventEnvelope)} to derive partition key.");
        }

        return PublishAsync(@event, envelope.GetPartitionKey(), cancellationToken);
    }

    private static string ResolveTopic<TEvent>() where TEvent : IDomainEvent =>
        typeof(TEvent) == typeof(OrderPlaced) ? EventTopics.OrderPlaced :
        typeof(TEvent) == typeof(PaymentAuthorized) ? EventTopics.PaymentAuthorized :
        typeof(TEvent) == typeof(PaymentFailed) ? EventTopics.PaymentFailed :
        typeof(TEvent) == typeof(OrderCancelled) ? EventTopics.OrderCancelled :
        typeof(TEvent) == typeof(ShipmentPrepared) ? EventTopics.ShipmentPrepared :
        throw new NotSupportedException($"Unsupported event type: {typeof(TEvent).Name}");
}
