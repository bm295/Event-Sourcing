using System.Text.Json;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class PaymentProcessingRecord
{
    public Guid InputEventId { get; set; }
    public required string OutcomeType { get; set; }
    public required string OutcomeJson { get; set; }
    public bool Completed { get; set; }

    public PaymentProcessingState ToState() => new(OutcomeType switch
    {
        nameof(PaymentAuthorized) => JsonSerializer.Deserialize<PaymentAuthorized>(OutcomeJson)!,
        nameof(PaymentFailed) => JsonSerializer.Deserialize<PaymentFailed>(OutcomeJson)!,
        _ => throw new InvalidOperationException($"Unknown payment outcome: {OutcomeType}")
    }, Completed);
}

public sealed class EfCorePaymentProcessingStore(EcommerceDbContext dbContext) : IPaymentProcessingStore
{
    public async Task<PaymentProcessingState?> FindAsync(Guid inputEventId)
    {
        var record = await dbContext.PaymentProcessing.AsNoTracking()
            .SingleOrDefaultAsync(x => x.InputEventId == inputEventId);
        return record?.ToState();
    }

    public async Task<PaymentProcessingState> SaveOutcomeAsync(OrderPlaced source, string? declineReason)
    {
        // Allocate the sequence and persist the outcome in one local transaction.
        // SQLite serializes writers; a lock/conflict bubbles up for broker retry.
        using var transaction = await dbContext.Database.BeginTransactionAsync();
        var existing = await FindAsync(source.EventId);
        if (existing is not null) return existing;

        var sequence = await dbContext.OrderEventSequences.SingleOrDefaultAsync(x => x.OrderId == source.OrderId);
        if (sequence is null)
        {
            sequence = new OrderEventSequenceRecord { OrderId = source.OrderId };
            dbContext.OrderEventSequences.Add(sequence);
        }
        sequence.LastSequenceNumber = Math.Max(sequence.LastSequenceNumber, source.SequenceNumber) + 1;
        var type = declineReason is null ? nameof(PaymentAuthorized) : nameof(PaymentFailed);
        var metadata = EventMetadata.NewChild(type, source, sequence.LastSequenceNumber);
        IDomainEvent outcome = declineReason is null
            ? new PaymentAuthorized(metadata.EventId, metadata.OccurredAt, metadata.CorrelationId,
                metadata.CausationId, type, source.OrderId, metadata.SequenceNumber, source.CustomerId, source.TotalAmount)
            : new PaymentFailed(metadata.EventId, metadata.OccurredAt, metadata.CorrelationId,
                metadata.CausationId, type, source.OrderId, metadata.SequenceNumber, source.CustomerId, source.TotalAmount, declineReason);
        var record = new PaymentProcessingRecord
        {
            InputEventId = source.EventId,
            OutcomeType = type,
            OutcomeJson = JsonSerializer.Serialize(outcome, outcome.GetType())
        };
        dbContext.PaymentProcessing.Add(record);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return record.ToState();
    }

    public async Task CompleteAsync(Guid inputEventId)
    {
        var updated = await dbContext.PaymentProcessing.Where(x => x.InputEventId == inputEventId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Completed, true));
        if (updated != 1) throw new InvalidOperationException("Cannot complete payment without a durable outcome.");
    }
}
