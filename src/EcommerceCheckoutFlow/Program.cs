using EcommerceCheckoutFlow.Adapters.Primary;
using EcommerceCheckoutFlow.Adapters.Secondary;
using EcommerceCheckoutFlow.Application.Handlers;
using EcommerceCheckoutFlow.Application.UseCases;

Console.WriteLine("=== Hexagonal + Event-Driven E-Commerce Demo ===");

// Secondary adapters (infrastructure implementations of outbound ports).
var inventoryAdapter = new InMemoryInventoryAdapter();
var paymentAdapter = new InMemoryPaymentAdapter();
var shippingAdapter = new InMemoryShippingAdapter();
var analyticsAdapter = new InMemoryAnalyticsAdapter();
var notificationAdapter = new ConsoleNotificationAdapter();

var eventBus = new InMemoryEventBus();

// Application services / use cases.
var checkoutUseCase = new CheckoutUseCase(eventBus);

// Application event handlers.
var inventoryHandler = new InventoryOnOrderPlacedHandler(inventoryAdapter);
var paymentHandler = new PaymentOnOrderPlacedHandler(paymentAdapter, eventBus);
var analyticsHandler = new AnalyticsOnOrderPlacedHandler(analyticsAdapter);
var shippingHandler = new ShippingOnPaymentAuthorizedHandler(shippingAdapter, eventBus);
var paymentNotificationHandler = new NotifyOnPaymentAuthorizedHandler(notificationAdapter);
var shipmentNotificationHandler = new NotifyOnShipmentPreparedHandler(notificationAdapter);

// Wiring events (domain/application boundary stays decoupled through ports + events).
eventBus.Subscribe<EcommerceCheckoutFlow.Domain.OrderPlaced>(inventoryHandler.Handle);
eventBus.Subscribe<EcommerceCheckoutFlow.Domain.OrderPlaced>(paymentHandler.Handle);
eventBus.Subscribe<EcommerceCheckoutFlow.Domain.OrderPlaced>(analyticsHandler.Handle);
eventBus.Subscribe<EcommerceCheckoutFlow.Domain.PaymentAuthorized>(shippingHandler.Handle);
eventBus.Subscribe<EcommerceCheckoutFlow.Domain.PaymentAuthorized>(paymentNotificationHandler.Handle);
eventBus.Subscribe<EcommerceCheckoutFlow.Domain.ShipmentPrepared>(shipmentNotificationHandler.Handle);

// Primary adapter triggers the use case.
var cliAdapter = new CheckoutCliAdapter(checkoutUseCase);
cliAdapter.RunDemo();

Console.WriteLine();
Console.WriteLine("=== Projection / Adapter State ===");
Console.WriteLine($"Reserved units: {inventoryAdapter.TotalReservedUnits}");
Console.WriteLine($"Authorized amount: ${paymentAdapter.TotalAuthorized}");
Console.WriteLine($"Shipments prepared: {shippingAdapter.ShipmentsPrepared}");
Console.WriteLine($"Orders tracked: {analyticsAdapter.OrdersTracked}");
Console.WriteLine($"Revenue tracked: ${analyticsAdapter.RevenueTracked}");
Console.WriteLine($"Notifications sent: {notificationAdapter.SentMessages}");
