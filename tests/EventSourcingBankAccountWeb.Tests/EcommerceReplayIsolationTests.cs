using EcommerceCheckoutFlow.Adapters.Secondary;
using EcommerceCheckoutFlow.Application.Projectors;
using EcommerceCheckoutFlow.Domain;
using Xunit;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class EcommerceReplayIsolationTests
{
    [Fact]
    public void RebuildState_replays_events_without_triggering_external_effect_adapters()
    {
        var inventory = new InMemoryInventoryAdapter();
        var payment = new InMemoryPaymentAdapter();
        var shipping = new InMemoryShippingAdapter();
        var notification = new ConsoleNotificationAdapter();

        var orderPlaced = CreateOrderPlaced("order-42", totalAmount: 30m, quantity: 3);
        var paymentAuthorized = CreatePaymentAuthorized(orderPlaced);
        var shipmentPrepared = CreateShipmentPrepared(paymentAuthorized);

        var replayEvents = new IDomainEvent[]
        {
            orderPlaced,
            paymentAuthorized,
            shipmentPrepared,
            orderPlaced,
            paymentAuthorized,
            shipmentPrepared
        };

        var rebuildService = new RebuildStateService(new CheckoutReadModelProjector());
        var readModel = rebuildService.Rebuild(replayEvents);

        Assert.Equal(0, notification.SentMessages);
        Assert.Equal(0, shipping.ShipmentsPrepared);
        Assert.Equal(0m, payment.TotalAuthorized);
        Assert.Equal(0, inventory.TotalReservedUnits);

        Assert.Equal(2, readModel.OrdersPlaced);
        Assert.Equal(6, readModel.TotalReservedUnits);
        Assert.Equal(60m, readModel.TotalAuthorized);
        Assert.Equal(2, readModel.ShipmentsPrepared);
    }

    private static OrderPlaced CreateOrderPlaced(string orderId, decimal totalAmount, int quantity)
    {
        var metadata = EventMetadata.NewRoot(nameof(OrderPlaced), orderId, 1);
        return new OrderPlaced(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            metadata.SequenceNumber,
            "customer-1",
            [new CartItem("sku-1", "Item 1", quantity, 10m)],
            totalAmount);
    }

    private static PaymentAuthorized CreatePaymentAuthorized(OrderPlaced orderPlaced)
    {
        var metadata = EventMetadata.NewChild(nameof(PaymentAuthorized), orderPlaced, 2);
        return new PaymentAuthorized(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            metadata.SequenceNumber,
            orderPlaced.CustomerId,
            orderPlaced.TotalAmount);
    }

    private static ShipmentPrepared CreateShipmentPrepared(PaymentAuthorized paymentAuthorized)
    {
        var metadata = EventMetadata.NewChild(nameof(ShipmentPrepared), paymentAuthorized, 1);
        return new ShipmentPrepared(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            metadata.SequenceNumber,
            paymentAuthorized.CustomerId,
            1);
    }
}
