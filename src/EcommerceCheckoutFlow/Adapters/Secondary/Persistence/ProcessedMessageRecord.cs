using EcommerceCheckoutFlow.Application.Ports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class ProcessedMessageRecord
{
    public long Id { get; init; }
    public required string ConsumerName { get; init; }
    public Guid EventId { get; init; }
    public DateTimeOffset ProcessedAtUtc { get; init; }
}

public sealed class EfCoreMessageDeduplicationStore(
    EcommerceDbContext dbContext,
    ILogger<EfCoreMessageDeduplicationStore> logger) : IMessageDeduplicationStore
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
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            dbContext.Entry(record).State = EntityState.Detached;
            logger.LogInformation(
                "Skipping duplicate processed message for consumer {ConsumerName} and event {EventId}",
                consumerName,
                eventId);
            return false;
        }
        catch (Exception ex)
        {
            dbContext.Entry(record).State = EntityState.Detached;
            logger.LogError(
                ex,
                "Failed to mark processed message for consumer {ConsumerName} and event {EventId}",
                consumerName,
                eventId);
            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
    }
}
