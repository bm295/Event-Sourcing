using EcommerceCheckoutFlow.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class OrderEventSequenceRecord
{
    public required string OrderId { get; init; }
    public long LastSequenceNumber { get; set; }
}

public sealed class ConsumerOrderSequenceRecord
{
    public long Id { get; init; }
    public required string ConsumerName { get; init; }
    public required string OrderId { get; init; }
    public long LastSequenceNumber { get; set; }
}

public sealed class EfCoreOrderEventSequenceAllocator(EcommerceDbContext dbContext) : IOrderEventSequenceAllocator
{
    public async Task<long> AllocateNextSequenceAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.OrderEventSequences.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (record is null)
        {
            record = new OrderEventSequenceRecord { OrderId = orderId, LastSequenceNumber = 0 };
            dbContext.OrderEventSequences.Add(record);
        }

        record.LastSequenceNumber++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return record.LastSequenceNumber;
    }
}

public sealed class EfCoreConsumerSequenceGuardStore(EcommerceDbContext dbContext) : IConsumerSequenceGuardStore
{
    public async Task<SequenceGuardDecision> CheckAndRecordAsync(string consumerName, string orderId, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.ConsumerOrderSequences.SingleOrDefaultAsync(x => x.ConsumerName == consumerName && x.OrderId == orderId, cancellationToken);
        if (record is null)
        {
            if (sequenceNumber != 1) return SequenceGuardDecision.OutOfOrder;
            dbContext.ConsumerOrderSequences.Add(new ConsumerOrderSequenceRecord { ConsumerName = consumerName, OrderId = orderId, LastSequenceNumber = sequenceNumber });
            await dbContext.SaveChangesAsync(cancellationToken);
            return SequenceGuardDecision.Accept;
        }

        if (sequenceNumber <= record.LastSequenceNumber) return SequenceGuardDecision.Duplicate;
        if (sequenceNumber != record.LastSequenceNumber + 1) return SequenceGuardDecision.OutOfOrder;
        record.LastSequenceNumber = sequenceNumber;
        await dbContext.SaveChangesAsync(cancellationToken);
        return SequenceGuardDecision.Accept;
    }
}
