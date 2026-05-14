using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Projectors;

public interface IReplayStateRebuilder
{
    CheckoutReadModel Rebuild(IEnumerable<IDomainEvent> eventStream);
}
