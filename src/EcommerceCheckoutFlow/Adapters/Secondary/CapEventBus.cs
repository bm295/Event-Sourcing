using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Secondary;

public sealed class CapEventBus(ICapPublisher capPublisher) : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var topic = ResolveTopic<TEvent>();
        return capPublisher.PublishAsync(topic, @event, cancellationToken: cancellationToken);
    }

    private static string ResolveTopic<TEvent>() where TEvent : IDomainEvent =>
        typeof(TEvent) == typeof(OrderPlaced) ? EventTopics.OrderPlaced :
        typeof(TEvent) == typeof(PaymentAuthorized) ? EventTopics.PaymentAuthorized :
        typeof(TEvent) == typeof(ShipmentPrepared) ? EventTopics.ShipmentPrepared :
        throw new NotSupportedException($"Unsupported event type: {typeof(TEvent).Name}");
}
