using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Projectors;

public sealed class RebuildStateService(CheckoutReadModelProjector projector) : IReplayStateRebuilder
{
    public CheckoutReadModel Rebuild(IEnumerable<IDomainEvent> eventStream)
    {
        var envelopes = eventStream.Cast<IEventEnvelope>()
            .OrderBy(x => x.OrderId)
            .ThenBy(x => x.SequenceNumber)
            .ToList();

        Validate(envelopes);

        var readModel = new CheckoutReadModel();
        foreach (var @event in envelopes)
        {
            projector.Project(readModel, (IDomainEvent)@event);
        }

        return readModel;
    }

    private static void Validate(IReadOnlyList<IEventEnvelope> events)
    {
        foreach (var grp in events.GroupBy(x => x.OrderId))
        {
            long expected = 1;
            foreach (var e in grp)
            {
                if (e.SequenceNumber != expected)
                    throw new InvalidOperationException($"Replay stream invalid for order {grp.Key}: expected sequence {expected}, got {e.SequenceNumber}.");
                expected++;
            }
        }
    }
}
