using DotNetCore.CAP;
using EcommerceCheckoutFlow.Adapters.Secondary.Persistence;
using EcommerceCheckoutFlow.Application;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.UseCases;

public sealed class CheckoutUseCase(EcommerceDbContext dbContext, ICapPublisher capPublisher)
{
    public async Task PlaceOrderAsync(
        string orderId,
        string customerId,
        IReadOnlyList<CartItem> items,
        CancellationToken cancellationToken = default)
    {
        var order = Order.Create(orderId, customerId, items);

        var orderPlaced = new OrderPlaced(
            order.OrderId,
            order.CustomerId,
            order.Items,
            order.TotalAmount,
            DateTimeOffset.UtcNow);

        using var transaction = dbContext.Database.BeginTransaction(capPublisher, autoCommit: false);

        dbContext.Orders.Add(OrderRecord.From(order));
        await dbContext.SaveChangesAsync(cancellationToken);

        await capPublisher.PublishAsync(EventTopics.OrderPlaced, orderPlaced, cancellationToken: cancellationToken);

        transaction.Commit();
    }
}
