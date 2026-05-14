using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Projectors;

public sealed class RebuildStateService(CheckoutReadModelProjector projector) : IReplayStateRebuilder
{
    public CheckoutReadModel Rebuild(IEnumerable<IDomainEvent> eventStream)
    {
        var readModel = new CheckoutReadModel();
        foreach (var @event in eventStream)
        {
            projector.Project(readModel, @event);
        }

        return readModel;
    }
}
