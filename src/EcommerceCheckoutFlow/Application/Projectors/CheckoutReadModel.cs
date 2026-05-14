using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Projectors;

public sealed class CheckoutReadModel
{
    public int OrdersPlaced { get; private set; }
    public int TotalReservedUnits { get; private set; }
    public decimal TotalAuthorized { get; private set; }
    public int ShipmentsPrepared { get; private set; }
    public int FailedPayments { get; private set; }
    public int CancelledOrders { get; private set; }

    public void Apply(OrderPlaced @event)
    {
        OrdersPlaced++;
        TotalReservedUnits += @event.Items.Sum(item => item.Quantity);
    }

    public void Apply(PaymentAuthorized @event) => TotalAuthorized += @event.Amount;

    public void Apply(ShipmentPrepared @event) => ShipmentsPrepared += @event.PackageCount;

    public void Apply(PaymentFailed @event) => FailedPayments++;

    public void Apply(OrderCancelled @event) => CancelledOrders++;
}
