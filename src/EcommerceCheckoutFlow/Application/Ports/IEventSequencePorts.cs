using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EcommerceCheckoutFlow.Application.Ports;

public interface IOrderEventSequenceAllocator
{
    Task<long> AllocateNextSequenceAsync(string orderId, CancellationToken cancellationToken = default);
}

public interface ICapTransactionCoordinator
{
    IDbContextTransaction BeginTransaction(DbContext dbContext, bool autoCommit = false);
}

public enum SequenceGuardDecision
{
    Accept,
    Duplicate,
    OutOfOrder
}

public interface IConsumerSequenceGuardStore
{
    Task<SequenceGuardDecision> CheckAndRecordAsync(
        string consumerName,
        string orderId,
        long sequenceNumber,
        CancellationToken cancellationToken = default);
}
