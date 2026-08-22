using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class EfCoreOrderStore(EcommerceDbContext dbContext) : IOrderStore
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        dbContext.Orders.Add(OrderRecord.From(order));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
