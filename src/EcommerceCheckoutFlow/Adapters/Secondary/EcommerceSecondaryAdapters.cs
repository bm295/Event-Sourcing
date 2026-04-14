using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Secondary;

public sealed class InMemoryInventoryAdapter : IInventoryPort
{
    public int TotalReservedUnits { get; private set; }

    public void ReserveItems(OrderPlaced @event)
    {
        var units = @event.Items.Sum(item => item.Quantity);
        TotalReservedUnits += units;
        Console.WriteLine($"[Inventory Adapter] Reserved {units} units for {@event.OrderId}.");
    }
}

public sealed class InMemoryPaymentAdapter : IPaymentPort
{
    public decimal TotalAuthorized { get; private set; }

    public void Authorize(OrderPlaced @event)
    {
        TotalAuthorized += @event.TotalAmount;
        Console.WriteLine($"[Payment Adapter] Authorized ${@event.TotalAmount} for {@event.OrderId}.");
    }
}

public sealed class InMemoryShippingAdapter : IShippingPort
{
    public int ShipmentsPrepared { get; private set; }

    public void Prepare(PaymentAuthorized @event)
    {
        ShipmentsPrepared++;
        Console.WriteLine($"[Shipping Adapter] Prepared shipment for {@event.OrderId}.");
    }
}

public sealed class InMemoryAnalyticsAdapter : IAnalyticsPort
{
    public int OrdersTracked { get; private set; }
    public decimal RevenueTracked { get; private set; }

    public void TrackOrder(OrderPlaced @event)
    {
        OrdersTracked++;
        RevenueTracked += @event.TotalAmount;
        Console.WriteLine($"[Analytics Adapter] Tracked {@event.OrderId} (${@event.TotalAmount}).");
    }
}

public sealed class ConsoleNotificationAdapter : INotificationPort
{
    public int SentMessages { get; private set; }

    public void Send(string message)
    {
        SentMessages++;
        Console.WriteLine($"[Notification Adapter] {message}");
    }
}
