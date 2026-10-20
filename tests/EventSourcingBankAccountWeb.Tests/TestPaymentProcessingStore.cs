using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EventSourcingBankAccountWeb.Tests;

internal sealed class TestPaymentProcessingStore : IPaymentProcessingStore
{
    private readonly Dictionary<Guid, PaymentProcessingState> states = new();
    public Task<PaymentProcessingState?> FindAsync(Guid id) => Task.FromResult(states.GetValueOrDefault(id));
    public Task<PaymentProcessingState> SaveOutcomeAsync(OrderPlaced source, string? declineReason)
    {
        if (states.TryGetValue(source.EventId, out var existing)) return Task.FromResult(existing);
        var type = declineReason is null ? nameof(PaymentAuthorized) : nameof(PaymentFailed);
        var m = EventMetadata.NewChild(type, source, source.SequenceNumber + 1);
        IDomainEvent outcome = declineReason is null
            ? new PaymentAuthorized(m.EventId, m.OccurredAt, m.CorrelationId, m.CausationId, type, m.OrderId, m.SequenceNumber, source.CustomerId, source.TotalAmount)
            : new PaymentFailed(m.EventId, m.OccurredAt, m.CorrelationId, m.CausationId, type, m.OrderId, m.SequenceNumber, source.CustomerId, source.TotalAmount, declineReason);
        var state = new PaymentProcessingState(outcome, false);
        states[source.EventId] = state;
        return Task.FromResult(state);
    }
    public Task CompleteAsync(Guid id)
    {
        states[id] = states[id] with { Completed = true };
        return Task.CompletedTask;
    }
}
