using System.Collections.Concurrent;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Secondary;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            return;
        }

        foreach (var handler in handlers.Cast<Action<TEvent>>())
        {
            handler(@event);
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
    {
        var handlers = _handlers.GetOrAdd(typeof(TEvent), _ => []);

        lock (handlers)
        {
            handlers.Add(handler);
        }
    }
}
