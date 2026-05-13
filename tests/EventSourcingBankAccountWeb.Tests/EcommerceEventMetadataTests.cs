using EcommerceCheckoutFlow.Application.Handlers;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class EcommerceEventMetadataTests
{
    [Fact]
    public async Task Published_events_have_non_empty_event_id_and_valid_metadata()
    {
        var eventBus = new CollectingEventBus();
        var paymentPort = new StubPaymentPort();
        var shippingPort = new StubShippingPort();

        var orderPlaced = CreateOrderPlaced();

        await new PaymentOnOrderPlacedHandler(paymentPort, eventBus).HandleAsync(orderPlaced);
        var paymentAuthorized = Assert.IsType<PaymentAuthorized>(Assert.Single(eventBus.Events));
        AssertValidMetadata(paymentAuthorized, nameof(PaymentAuthorized), orderPlaced);

        await new ShippingOnPaymentAuthorizedHandler(shippingPort, eventBus).HandleAsync(paymentAuthorized);
        var shipmentPrepared = Assert.IsType<ShipmentPrepared>(eventBus.Events.Last());
        AssertValidMetadata(shipmentPrepared, nameof(ShipmentPrepared), paymentAuthorized);
    }

    [Fact]
    public async Task Failure_flow_events_have_non_empty_event_id_and_valid_metadata()
    {
        var eventBus = new CollectingEventBus();
        var paymentPort = new StubPaymentPort(shouldThrow: true);

        var orderPlaced = CreateOrderPlaced();

        await new PaymentOnOrderPlacedHandler(paymentPort, eventBus).HandleAsync(orderPlaced);
        var paymentFailed = Assert.IsType<PaymentFailed>(Assert.Single(eventBus.Events));
        AssertValidMetadata(paymentFailed, nameof(PaymentFailed), orderPlaced);

        await new CancelOrderOnPaymentFailedHandler(eventBus).HandleAsync(paymentFailed);
        var orderCancelled = Assert.IsType<OrderCancelled>(eventBus.Events.Last());
        AssertValidMetadata(orderCancelled, nameof(OrderCancelled), paymentFailed);
    }

    private static OrderPlaced CreateOrderPlaced()
    {
        var metadata = EventMetadata.NewRoot(nameof(OrderPlaced), "order-123");

        return new OrderPlaced(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            "customer-456",
            [new CartItem("sku-1", "Item 1", 1, 9.99m)],
            9.99m);
    }

    private static void AssertValidMetadata(IEventEnvelope @event, string expectedType, IEventEnvelope cause)
    {
        Assert.NotEqual(Guid.Empty, @event.EventId);
        Assert.True(@event.OccurredAt <= DateTimeOffset.UtcNow);
        Assert.Equal(expectedType, @event.EventType);
        Assert.Equal(cause.OrderId, @event.OrderId);
        Assert.Equal(cause.CorrelationId, @event.CorrelationId);
        Assert.Equal(cause.EventId, @event.CausationId);
    }

    private sealed class CollectingEventBus : IEventBus
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IDomainEvent
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPaymentPort(bool shouldThrow = false) : IPaymentPort
    {
        public void Authorize(OrderPlaced @event)
        {
            if (shouldThrow)
            {
                throw new InvalidOperationException("card declined");
            }
        }
    }

    private sealed class StubShippingPort : IShippingPort
    {
        public void Prepare(PaymentAuthorized @event)
        {
        }
    }
}
