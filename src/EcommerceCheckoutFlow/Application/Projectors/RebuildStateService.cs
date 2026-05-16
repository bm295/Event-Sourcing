using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Projectors;

public sealed class RebuildStateService(CheckoutReadModelProjector projector) : IReplayStateRebuilder
{
    public CheckoutReadModel Rebuild(IEnumerable<IDomainEvent> eventStream)
    {
        var envelopes = eventStream.Cast<IEventEnvelope>().ToList();

        var readModel = new CheckoutReadModel();
        foreach (var @event in envelopes)
        {
            projector.Project(readModel, (IDomainEvent)@event);
        }

        return readModel;
    }
}
