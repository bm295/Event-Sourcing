using EcommerceCheckoutFlow.Application.Ports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class ProcessedMessageRecord
{
    public long Id { get; init; }
    public required string ConsumerName { get; init; }
    public Guid EventId { get; init; }
    public DateTimeOffset ProcessedAtUtc { get; init; }
}

public sealed class EfCoreMessageDeduplicationStore(EcommerceDbContext dbContext) : IMessageDeduplicationStore
{
    public async Task<bool> TryMarkProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default)
    {
        var record = new ProcessedMessageRecord
        {
            ConsumerName = consumerName,
            EventId = eventId,
            ProcessedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.ProcessedMessages.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            dbContext.Entry(record).State = EntityState.Detached;
            return false;
        }
    }
}
