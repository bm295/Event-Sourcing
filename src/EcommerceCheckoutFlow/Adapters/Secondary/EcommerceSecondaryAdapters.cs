using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Secondary;

public sealed class InMemoryInventoryAdapter : IInventoryPort
{
    public int TotalReservedUnits { get; private set; }
    private readonly HashSet<string> _processedKeys = [];

    public void ReserveItems(OrderPlaced @event, string idempotencyKey)
    {
        if (!_processedKeys.Add(idempotencyKey))
        {
            Console.WriteLine($"[Inventory Adapter] Skipped duplicate idempotency key: {idempotencyKey}");
            return;
        }

        var units = @event.Items.Sum(item => item.Quantity);
        TotalReservedUnits += units;
        Console.WriteLine($"[Inventory Adapter] Reserved {units} units for {@event.OrderId}. Key={idempotencyKey}");
    }
}

public sealed class InMemoryPaymentAdapter : IPaymentPort
{
    public decimal TotalAuthorized { get; private set; }
    private readonly HashSet<string> _processedKeys = [];

    public void Authorize(OrderPlaced @event, string idempotencyKey)
    {
        if (!_processedKeys.Add(idempotencyKey))
        {
            Console.WriteLine($"[Payment Adapter] Skipped duplicate idempotency key: {idempotencyKey}");
            return;
        }

        TotalAuthorized += @event.TotalAmount;
        Console.WriteLine($"[Payment Adapter] Authorized ${@event.TotalAmount} for {@event.OrderId}. Key={idempotencyKey}");
    }
}

public sealed class InMemoryShippingAdapter : IShippingPort
{
    public int ShipmentsPrepared { get; private set; }
    private readonly HashSet<string> _processedKeys = [];

    public void Prepare(PaymentAuthorized @event, string idempotencyKey)
    {
        if (!_processedKeys.Add(idempotencyKey))
        {
            Console.WriteLine($"[Shipping Adapter] Skipped duplicate idempotency key: {idempotencyKey}");
            return;
        }

        ShipmentsPrepared++;
        Console.WriteLine($"[Shipping Adapter] Prepared shipment for {@event.OrderId}. Key={idempotencyKey}");
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
    private readonly HashSet<string> _processedKeys = [];

    public void Send(string message, string idempotencyKey)
    {
        if (!_processedKeys.Add(idempotencyKey))
        {
            Console.WriteLine($"[Notification Adapter] Skipped duplicate idempotency key: {idempotencyKey}");
            return;
        }

        SentMessages++;
        Console.WriteLine($"[Notification Adapter] {message} Key={idempotencyKey}");
    }
}
