namespace EcommerceCheckoutFlow.Application.Ports;

public interface IOrderEventSequenceAllocator
{
    Task<long> AllocateNextSequenceAsync(string orderId, CancellationToken cancellationToken = default);
}

public interface ICheckoutTransaction : IDisposable
{
    void Commit();
}

public interface ICheckoutTransactionManager
{
    ICheckoutTransaction Begin();
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
