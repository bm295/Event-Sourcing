using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Ports;

public interface IInventoryPort
{
    void ReserveItems(OrderPlaced @event, string idempotencyKey);
}

// Providers must persist idempotency by key, including across process restarts.
// Throw PaymentDeclinedException only for a definitive rejection. Other failures
// have unknown outcomes and are retried with the same key.
public interface IPaymentPort
{
    void Authorize(OrderPlaced @event, string idempotencyKey);
}

public interface IShippingPort
{
    void Prepare(PaymentAuthorized @event, string idempotencyKey);
}

public interface INotificationPort
{
    void Send(string message, string idempotencyKey);
}

public interface IAnalyticsPort
{
    void TrackOrder(OrderPlaced @event);
}
