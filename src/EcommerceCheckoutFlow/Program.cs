using DotNetCore.CAP;
using EcommerceCheckoutFlow.Adapters.Primary;
using EcommerceCheckoutFlow.Adapters.Secondary;
using EcommerceCheckoutFlow.Application.Handlers;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("=== Hexagonal + Event-Driven E-Commerce Demo (CAP) ===");

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton<InMemoryInventoryAdapter>()
    .AddSingleton<InMemoryPaymentAdapter>()
    .AddSingleton<InMemoryShippingAdapter>()
    .AddSingleton<InMemoryAnalyticsAdapter>()
    .AddSingleton<ConsoleNotificationAdapter>();

builder.Services
    .AddSingleton<IInventoryPort>(sp => sp.GetRequiredService<InMemoryInventoryAdapter>())
    .AddSingleton<IPaymentPort>(sp => sp.GetRequiredService<InMemoryPaymentAdapter>())
    .AddSingleton<IShippingPort>(sp => sp.GetRequiredService<InMemoryShippingAdapter>())
    .AddSingleton<IAnalyticsPort>(sp => sp.GetRequiredService<InMemoryAnalyticsAdapter>())
    .AddSingleton<INotificationPort>(sp => sp.GetRequiredService<ConsoleNotificationAdapter>());

builder.Services
    .AddSingleton<IEventBus, CapEventBus>()
    .AddSingleton<CheckoutUseCase>()
    .AddSingleton<CheckoutCliAdapter>()
    .AddSingleton<InventoryOnOrderPlacedHandler>()
    .AddSingleton<PaymentOnOrderPlacedHandler>()
    .AddSingleton<AnalyticsOnOrderPlacedHandler>()
    .AddSingleton<ShippingOnPaymentAuthorizedHandler>()
    .AddSingleton<NotifyOnPaymentAuthorizedHandler>()
    .AddSingleton<NotifyOnShipmentPreparedHandler>();

builder.Services.AddCap(capOptions =>
{
    capOptions.UseInMemoryQueue();
    capOptions.UseInMemoryStorage();
});

using var host = builder.Build();
await host.StartAsync();

var cliAdapter = host.Services.GetRequiredService<CheckoutCliAdapter>();
await cliAdapter.RunDemoAsync();

// CAP subscribers run in background hosted services.
await Task.Delay(300);

var inventoryAdapter = host.Services.GetRequiredService<InMemoryInventoryAdapter>();
var paymentAdapter = host.Services.GetRequiredService<InMemoryPaymentAdapter>();
var shippingAdapter = host.Services.GetRequiredService<InMemoryShippingAdapter>();
var analyticsAdapter = host.Services.GetRequiredService<InMemoryAnalyticsAdapter>();
var notificationAdapter = host.Services.GetRequiredService<ConsoleNotificationAdapter>();

Console.WriteLine();
Console.WriteLine("=== Projection / Adapter State ===");
Console.WriteLine($"Reserved units: {inventoryAdapter.TotalReservedUnits}");
Console.WriteLine($"Authorized amount: ${paymentAdapter.TotalAuthorized}");
Console.WriteLine($"Shipments prepared: {shippingAdapter.ShipmentsPrepared}");
Console.WriteLine($"Orders tracked: {analyticsAdapter.OrdersTracked}");
Console.WriteLine($"Revenue tracked: ${analyticsAdapter.RevenueTracked}");
Console.WriteLine($"Notifications sent: {notificationAdapter.SentMessages}");

await host.StopAsync();
