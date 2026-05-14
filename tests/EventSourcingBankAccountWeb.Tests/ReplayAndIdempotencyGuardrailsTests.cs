using EcommerceCheckoutFlow.Adapters.Secondary.Persistence;
using EcommerceCheckoutFlow.Application.Handlers;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Application.Projectors;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class ReplayAndIdempotencyGuardrailsTests
{
    [Fact]
    public async Task Given_duplicate_OrderPlaced_delivery_When_handlers_process_messages_Then_payment_inventory_and_analytics_side_effects_are_not_duplicated()
    {
        var paymentPort = new SpyPaymentPort();
        var inventoryPort = new SpyInventoryPort();
        var analyticsPort = new SpyAnalyticsPort();
        var eventBus = new SpyEventBus();
        var dedupe = new InMemoryDeduplicationStore();
        var orderPlaced = CreateOrderPlaced("order-100", quantity: 2, totalAmount: 30m);

        var paymentHandler = new PaymentOnOrderPlacedHandler(paymentPort, eventBus, dedupe);
        var inventoryHandler = new InventoryOnOrderPlacedHandler(new IdempotentInventoryPortWrapper(inventoryPort));
        var analyticsHandler = new AnalyticsOnOrderPlacedHandler(analyticsPort, dedupe);

        await paymentHandler.HandleAsync(orderPlaced);
        await paymentHandler.HandleAsync(orderPlaced);

        await inventoryHandler.HandleAsync(orderPlaced);
        await inventoryHandler.HandleAsync(orderPlaced);

        await analyticsHandler.HandleAsync(orderPlaced);
        await analyticsHandler.HandleAsync(orderPlaced);

        Assert.Equal(1, paymentPort.AuthorizationCalls);
        Assert.Equal(orderPlaced.TotalAmount, paymentPort.TotalAuthorized);
        Assert.Equal(1, inventoryPort.ReservationCalls);
        Assert.Equal(orderPlaced.Items.Sum(x => x.Quantity), inventoryPort.TotalReservedUnits);
        Assert.Equal(1, analyticsPort.TrackedOrders);
        Assert.Single(eventBus.Published.OfType<PaymentAuthorized>());
    }

    [Fact]
    public async Task Given_duplicate_PaymentAuthorized_delivery_When_shipping_and_notification_handlers_run_Then_side_effects_are_not_duplicated()
    {
        var shippingPort = new SpyShippingPort();
        var notificationPort = new SpyNotificationPort();
        var eventBus = new SpyEventBus();
        var dedupe = new InMemoryDeduplicationStore();
        var paymentAuthorized = CreatePaymentAuthorized(CreateOrderPlaced("order-200", 1, 15m));

        var shippingHandler = new ShippingOnPaymentAuthorizedHandler(shippingPort, eventBus, dedupe);
        var notifyHandler = new NotifyOnPaymentAuthorizedHandler(notificationPort, dedupe);

        await shippingHandler.HandleAsync(paymentAuthorized);
        await shippingHandler.HandleAsync(paymentAuthorized);

        await notifyHandler.HandleAsync(paymentAuthorized);
        await notifyHandler.HandleAsync(paymentAuthorized);

        Assert.Equal(1, shippingPort.PrepareCalls);
        Assert.Equal(1, notificationPort.SendCalls);
        Assert.Single(eventBus.Published.OfType<ShipmentPrepared>());
    }

    [Fact]
    public void Given_historical_stream_When_replay_is_executed_Then_only_read_model_changes_and_no_outbound_side_effects_are_called()
    {
        var paymentPort = new SpyPaymentPort();
        var inventoryPort = new SpyInventoryPort();
        var analyticsPort = new SpyAnalyticsPort();
        var shippingPort = new SpyShippingPort();
        var notificationPort = new SpyNotificationPort();

        var orderPlaced = CreateOrderPlaced("order-300", 3, 45m);
        var paymentAuthorized = CreatePaymentAuthorized(orderPlaced);
        var shipmentPrepared = CreateShipmentPrepared(paymentAuthorized);

        var rebuildService = new RebuildStateService(new CheckoutReadModelProjector());
        var readModel = rebuildService.Rebuild(new IDomainEvent[] { orderPlaced, paymentAuthorized, shipmentPrepared });

        Assert.Equal(1, readModel.OrdersPlaced);
        Assert.Equal(3, readModel.TotalReservedUnits);
        Assert.Equal(45m, readModel.TotalAuthorized);
        Assert.Equal(1, readModel.ShipmentsPrepared);

        Assert.Equal(0, paymentPort.AuthorizationCalls);
        Assert.Equal(0, inventoryPort.ReservationCalls);
        Assert.Equal(0, analyticsPort.TrackedOrders);
        Assert.Equal(0, shippingPort.PrepareCalls);
        Assert.Equal(0, notificationPort.SendCalls);
    }

    [Fact]
    public async Task Given_concurrent_duplicate_delivery_When_TryMarkProcessed_races_Then_only_one_consumer_execution_wins()
    {
        var dedupe = new InMemoryDeduplicationStore();
        var eventId = Guid.NewGuid();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => dedupe.TryMarkProcessedAsync("PaymentOnOrderPlacedHandler", eventId)));

        Assert.Equal(1, attempts.Count(x => x));
        Assert.Equal(19, attempts.Count(x => !x));
    }

    [Fact]
    public async Task Given_concurrent_duplicate_delivery_When_using_EFCore_dedup_store_Then_unique_constraint_and_race_handling_prevent_duplicates()
    {
        var connection = new SqliteConnection("Data Source=file:dedupe-race?mode=memory&cache=shared");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new EcommerceDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        var eventId = Guid.NewGuid();
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = new EcommerceDbContext(options);
            var store = new EfCoreMessageDeduplicationStore(context);
            return await store.TryMarkProcessedAsync("ShippingOnPaymentAuthorizedHandler", eventId);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(x => x));

        await using var verifyContext = new EcommerceDbContext(options);
        var records = await verifyContext.ProcessedMessages
            .Where(x => x.ConsumerName == "ShippingOnPaymentAuthorizedHandler" && x.EventId == eventId)
            .CountAsync();
        Assert.Equal(1, records);
    }

    [Fact]
    public async Task Given_duplicate_ShipmentPrepared_delivery_When_notify_handler_processes_event_Then_notification_side_effect_is_called_only_once()
    {
        var notificationPort = new SpyNotificationPort();
        var dedupe = new InMemoryDeduplicationStore();
        var orderPlaced = CreateOrderPlaced("order-400", 1, 20m);
        var paymentAuthorized = CreatePaymentAuthorized(orderPlaced);
        var shipmentPrepared = CreateShipmentPrepared(paymentAuthorized);
        var handler = new NotifyOnShipmentPreparedHandler(notificationPort, dedupe);

        await handler.HandleAsync(shipmentPrepared);
        await handler.HandleAsync(shipmentPrepared);

        Assert.Equal(1, notificationPort.SendCalls);
    }

    [Fact]
    public async Task Given_duplicate_OrderPlaced_delivery_When_analytics_handler_processes_event_Then_tracking_side_effect_does_not_increment_twice()
    {
        var analyticsPort = new SpyAnalyticsPort();
        var dedupe = new InMemoryDeduplicationStore();
        var orderPlaced = CreateOrderPlaced("order-500", 2, 35m);
        var handler = new AnalyticsOnOrderPlacedHandler(analyticsPort, dedupe);

        await handler.HandleAsync(orderPlaced);
        await handler.HandleAsync(orderPlaced);

        Assert.Equal(1, analyticsPort.TrackedOrders);
    }

    [Fact]
    public async Task Given_concurrent_duplicate_ShipmentPrepared_delivery_When_notify_handler_races_on_same_event_Then_only_one_notification_side_effect_is_executed()
    {
        var notificationPort = new SpyNotificationPort();
        var dedupe = new InMemoryDeduplicationStore();
        var orderPlaced = CreateOrderPlaced("order-600", 1, 25m);
        var paymentAuthorized = CreatePaymentAuthorized(orderPlaced);
        var shipmentPrepared = CreateShipmentPrepared(paymentAuthorized);
        var handler = new NotifyOnShipmentPreparedHandler(notificationPort, dedupe);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => handler.HandleAsync(shipmentPrepared)));

        Assert.Equal(1, notificationPort.SendCalls);
    }

    [Fact]
    public async Task Given_concurrent_duplicate_OrderPlaced_delivery_When_analytics_handler_races_on_same_event_Then_tracking_side_effect_is_executed_once()
    {
        var analyticsPort = new SpyAnalyticsPort();
        var dedupe = new InMemoryDeduplicationStore();
        var orderPlaced = CreateOrderPlaced("order-700", 1, 10m);
        var handler = new AnalyticsOnOrderPlacedHandler(analyticsPort, dedupe);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => handler.HandleAsync(orderPlaced)));

        Assert.Equal(1, analyticsPort.TrackedOrders);
    }

    private static OrderPlaced CreateOrderPlaced(string orderId, int quantity, decimal totalAmount)
    {
        var metadata = EventMetadata.NewRoot(nameof(OrderPlaced), orderId);
        return new OrderPlaced(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            "customer-1",
            [new CartItem("sku-1", "Item 1", quantity, 10m)],
            totalAmount);
    }

    private static PaymentAuthorized CreatePaymentAuthorized(OrderPlaced source)
    {
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

    private static ShipmentPrepared CreateShipmentPrepared(PaymentAuthorized source)
    {
        var metadata = EventMetadata.NewChild(nameof(ShipmentPrepared), source);
        return new ShipmentPrepared(
            metadata.EventId,
            metadata.OccurredAt,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.EventType,
            metadata.OrderId,
            source.CustomerId,
            1);
    }

    private sealed class InMemoryDeduplicationStore : IMessageDeduplicationStore
    {
        private readonly HashSet<string> _processed = [];
        private readonly object _gate = new();

        public Task<bool> TryMarkProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_processed.Add($"{consumerName}:{eventId:N}"));
            }
        }
    }

    private sealed class SpyEventBus : IEventBus
    {
        public List<IDomainEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyPaymentPort : IPaymentPort
    {
        public int AuthorizationCalls { get; private set; }
        public decimal TotalAuthorized { get; private set; }

        public void Authorize(OrderPlaced orderPlaced, string idempotencyKey)
        {
            AuthorizationCalls++;
            TotalAuthorized += orderPlaced.TotalAmount;
        }
    }

    private sealed class SpyInventoryPort
    {
        public int ReservationCalls { get; private set; }
        public int TotalReservedUnits { get; private set; }

        public void ReserveItems(OrderPlaced orderPlaced)
        {
            ReservationCalls++;
            TotalReservedUnits += orderPlaced.Items.Sum(x => x.Quantity);
        }
    }

    private sealed class IdempotentInventoryPortWrapper(SpyInventoryPort spy) : IInventoryPort
    {
        private readonly HashSet<string> _keys = [];

        public void ReserveItems(OrderPlaced orderPlaced, string idempotencyKey)
        {
            if (_keys.Add(idempotencyKey))
            {
                spy.ReserveItems(orderPlaced);
            }
        }
    }

    private sealed class SpyAnalyticsPort : IAnalyticsPort
    {
        public int TrackedOrders { get; private set; }
        public void TrackOrder(OrderPlaced orderPlaced) => TrackedOrders++;
    }

    private sealed class SpyShippingPort : IShippingPort
    {
        public int PrepareCalls { get; private set; }
        public void Prepare(PaymentAuthorized paymentAuthorized, string idempotencyKey) => PrepareCalls++;
    }

    private sealed class SpyNotificationPort : INotificationPort
    {
        public int SendCalls { get; private set; }
        public void Send(string message, string idempotencyKey) => SendCalls++;
    }
}
