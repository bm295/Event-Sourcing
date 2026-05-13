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

        var metadata = EventMetadata.NewRoot(nameof(OrderPlaced), order.OrderId);
        var orderPlaced = new OrderPlaced(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            order.CustomerId,
            order.Items,
            order.TotalAmount);

        using var transaction = dbContext.Database.BeginTransaction(capPublisher, autoCommit: false);

        dbContext.Orders.Add(OrderRecord.From(order));
        await dbContext.SaveChangesAsync(cancellationToken);

        await capPublisher.PublishAsync(EventTopics.OrderPlaced, orderPlaced, cancellationToken: cancellationToken);

        transaction.Commit();
    }
}
