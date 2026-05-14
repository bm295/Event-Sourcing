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
        var dedup = new InMemoryDeduplicationStore();

        var orderPlaced = CreateOrderPlaced();

        await new PaymentOnOrderPlacedHandler(paymentPort, eventBus, dedup).HandleAsync(orderPlaced);
        var paymentAuthorized = Assert.IsType<PaymentAuthorized>(Assert.Single(eventBus.Events));
        AssertValidMetadata(paymentAuthorized, nameof(PaymentAuthorized), orderPlaced);

        await new ShippingOnPaymentAuthorizedHandler(shippingPort, eventBus, dedup).HandleAsync(paymentAuthorized);
        var shipmentPrepared = Assert.IsType<ShipmentPrepared>(eventBus.Events.Last());
        AssertValidMetadata(shipmentPrepared, nameof(ShipmentPrepared), paymentAuthorized);
    }

    [Fact]
    public async Task Failure_flow_events_have_non_empty_event_id_and_valid_metadata()
    {
        var eventBus = new CollectingEventBus();
        var paymentPort = new StubPaymentPort(shouldThrow: true);
        var dedup = new InMemoryDeduplicationStore();

        var orderPlaced = CreateOrderPlaced();

        await new PaymentOnOrderPlacedHandler(paymentPort, eventBus, dedup).HandleAsync(orderPlaced);
        var paymentFailed = Assert.IsType<PaymentFailed>(Assert.Single(eventBus.Events));
        AssertValidMetadata(paymentFailed, nameof(PaymentFailed), orderPlaced);

        await new CancelOrderOnPaymentFailedHandler(eventBus).HandleAsync(paymentFailed);
        var orderCancelled = Assert.IsType<OrderCancelled>(eventBus.Events.Last());
        AssertValidMetadata(orderCancelled, nameof(OrderCancelled), paymentFailed);
    }

    [Fact]
    public async Task Duplicate_delivery_only_triggers_payment_side_effect_once()
    {
        var eventBus = new CollectingEventBus();
        var paymentPort = new StubPaymentPort();
        var dedup = new InMemoryDeduplicationStore();
        var handler = new PaymentOnOrderPlacedHandler(paymentPort, eventBus, dedup);
        var orderPlaced = CreateOrderPlaced();

        await handler.HandleAsync(orderPlaced);
        await handler.HandleAsync(orderPlaced);

        Assert.Equal(1, paymentPort.AuthorizeCalls);
        Assert.Single(eventBus.Events);
    }

    [Fact]
    public async Task Duplicate_delivery_only_triggers_shipping_side_effect_once()
    {
        var eventBus = new CollectingEventBus();
        var shippingPort = new StubShippingPort();
        var dedup = new InMemoryDeduplicationStore();
        var handler = new ShippingOnPaymentAuthorizedHandler(shippingPort, eventBus, dedup);
        var paymentAuthorized = CreatePaymentAuthorized();

        await handler.HandleAsync(paymentAuthorized);
        await handler.HandleAsync(paymentAuthorized);

        Assert.Equal(1, shippingPort.PrepareCalls);
        Assert.Single(eventBus.Events);
    }

    [Fact]
    public async Task Duplicate_delivery_only_triggers_notification_side_effect_once()
    {
        var notificationPort = new StubNotificationPort();
        var dedup = new InMemoryDeduplicationStore();
        var handler = new NotifyOnPaymentAuthorizedHandler(notificationPort, dedup);
        var paymentAuthorized = CreatePaymentAuthorized();

        await handler.HandleAsync(paymentAuthorized);
        await handler.HandleAsync(paymentAuthorized);

        Assert.Equal(1, notificationPort.SendCalls);
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

    private static PaymentAuthorized CreatePaymentAuthorized()
    {
        var orderPlaced = CreateOrderPlaced();
        var metadata = EventMetadata.NewChild(nameof(PaymentAuthorized), orderPlaced);

        return new PaymentAuthorized(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            orderPlaced.CustomerId,
            orderPlaced.TotalAmount);
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
        public int AuthorizeCalls { get; private set; }

        public void Authorize(OrderPlaced @event, string idempotencyKey)
        {
            AuthorizeCalls++;
            if (shouldThrow)
            {
                throw new InvalidOperationException("card declined");
            }
        }
    }

    private sealed class StubShippingPort : IShippingPort
    {
        public int PrepareCalls { get; private set; }

        public void Prepare(PaymentAuthorized @event, string idempotencyKey)
        {
            PrepareCalls++;
        }
    }

    private sealed class StubNotificationPort : INotificationPort
    {
        public int SendCalls { get; private set; }

        public void Send(string message, string idempotencyKey)
        {
            SendCalls++;
        }
    }

    private sealed class InMemoryDeduplicationStore : IMessageDeduplicationStore
    {
        private readonly HashSet<string> _processed = [];

        public Task<bool> TryMarkProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default)
        {
            var key = $"{consumerName}:{eventId}";
            return Task.FromResult(_processed.Add(key));
        }
    }
}
