using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Ports;

public interface IEventBus
{
    void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
}
