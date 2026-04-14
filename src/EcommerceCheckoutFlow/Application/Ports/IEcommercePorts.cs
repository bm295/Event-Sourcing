using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Ports;

public interface IInventoryPort
{
    void ReserveItems(OrderPlaced @event);
}

public interface IPaymentPort
{
    void Authorize(OrderPlaced @event);
}

public interface IShippingPort
{
    void Prepare(PaymentAuthorized @event);
}

public interface INotificationPort
{
    void Send(string message);
}

public interface IAnalyticsPort
{
    void TrackOrder(OrderPlaced @event);
}
