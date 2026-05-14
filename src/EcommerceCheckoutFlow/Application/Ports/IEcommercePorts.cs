using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Ports;

public interface IInventoryPort
{
    void ReserveItems(OrderPlaced @event, string idempotencyKey);
}

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
