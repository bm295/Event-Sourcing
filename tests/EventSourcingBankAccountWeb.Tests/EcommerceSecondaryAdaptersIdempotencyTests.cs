using EcommerceCheckoutFlow.Adapters.Secondary;
using EcommerceCheckoutFlow.Domain;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class EcommerceSecondaryAdaptersIdempotencyTests
{
    [Fact]
    public void Payment_adapter_processes_same_key_only_once()
    {
        var adapter = new InMemoryPaymentAdapter();
        var orderPlaced = CreateOrderPlaced();
        var key = $"Payment:{orderPlaced.EventId}";

        adapter.Authorize(orderPlaced, key);
        adapter.Authorize(orderPlaced, key);

        Assert.Equal(orderPlaced.TotalAmount, adapter.TotalAuthorized);
    }

    [Fact]
    public void Shipping_adapter_processes_same_key_only_once()
    {
        var adapter = new InMemoryShippingAdapter();
        var paymentAuthorized = CreatePaymentAuthorized();
        var key = $"Shipping:{paymentAuthorized.EventId}";

        adapter.Prepare(paymentAuthorized, key);
        adapter.Prepare(paymentAuthorized, key);

        Assert.Equal(1, adapter.ShipmentsPrepared);
    }

    [Fact]
    public void Inventory_adapter_processes_same_key_only_once()
    {
        var adapter = new InMemoryInventoryAdapter();
        var orderPlaced = CreateOrderPlaced();
        var key = $"Inventory:{orderPlaced.EventId}";

        adapter.ReserveItems(orderPlaced, key);
        adapter.ReserveItems(orderPlaced, key);

        Assert.Equal(orderPlaced.Items.Sum(i => i.Quantity), adapter.TotalReservedUnits);
    }

    [Fact]
    public void Notification_adapter_processes_same_key_only_once()
    {
        var adapter = new ConsoleNotificationAdapter();
        var key = "Notification:evt-1";

        adapter.Send("hello", key);
        adapter.Send("hello", key);

        Assert.Equal(1, adapter.SentMessages);
    }

    private static OrderPlaced CreateOrderPlaced()
    {
        var metadata = EventMetadata.NewRoot(nameof(OrderPlaced), "order-1");
        return new OrderPlaced(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            "customer-1",
            [new CartItem("sku-1", "Item 1", 2, 5m)],
            10m);
    }

    private static PaymentAuthorized CreatePaymentAuthorized()
    {
        var source = CreateOrderPlaced();
        var metadata = EventMetadata.NewChild(nameof(PaymentAuthorized), source);
        return new PaymentAuthorized(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            source.CustomerId,
            source.TotalAmount);
    }
}
