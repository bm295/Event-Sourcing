using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class PaymentAuthorizationRecord
{
    public required string IdempotencyKey { get; set; }
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
}

// Demo payment provider: the authorization itself and its idempotency key survive
// restart together. A real provider must guarantee the same contract remotely.
public sealed class PersistentPaymentAdapter(EcommerceDbContext dbContext) : IPaymentPort
{
    public void Authorize(OrderPlaced source, string idempotencyKey)
    {
        using var transaction = dbContext.Database.BeginTransaction();
        var existing = dbContext.PaymentAuthorizations.Find(idempotencyKey);
        if (existing is not null)
        {
            if (existing.OrderId != source.OrderId || existing.Amount != source.TotalAmount)
                throw new InvalidOperationException("Payment idempotency key reused for a different request.");
            return;
        }
        dbContext.PaymentAuthorizations.Add(new PaymentAuthorizationRecord
        {
            IdempotencyKey = idempotencyKey, OrderId = source.OrderId, Amount = source.TotalAmount
        });
        dbContext.SaveChanges();
        transaction.Commit();
    }
}
