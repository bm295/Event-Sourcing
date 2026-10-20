using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Ports;

// A durable inbox and outcome journal for the payment consumer. An outcome is
// immutable once saved; retries publish that same event before marking completion.
public interface IPaymentProcessingStore
{
    Task<PaymentProcessingState?> FindAsync(Guid inputEventId);
    Task<PaymentProcessingState> SaveOutcomeAsync(OrderPlaced source, string? declineReason);
    Task CompleteAsync(Guid inputEventId);
}

public sealed record PaymentProcessingState(IDomainEvent Outcome, bool Completed);
