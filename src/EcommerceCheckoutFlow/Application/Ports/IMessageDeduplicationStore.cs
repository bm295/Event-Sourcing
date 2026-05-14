namespace EcommerceCheckoutFlow.Application.Ports;

public interface IMessageDeduplicationStore
{
    Task<bool> TryMarkProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default);
}
