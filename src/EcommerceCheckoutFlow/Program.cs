using DotNetCore.CAP;
using EcommerceCheckoutFlow.Adapters.Primary;
using EcommerceCheckoutFlow.Adapters.Secondary;
using EcommerceCheckoutFlow.Adapters.Secondary.Persistence;
using EcommerceCheckoutFlow.Application.Handlers;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Application.UseCases;
using Microsoft.EntityFrameworkCore;

Console.WriteLine("=== Hexagonal + Event-Driven E-Commerce Demo (CAP) ===");

var builder = WebApplication.CreateBuilder(args);

var sqliteConnection = builder.Configuration["CHECKOUT_DB_CONNECTION"]
    ?? "Data Source=ecommerce-checkout.db";

var rabbitHost = builder.Configuration["CAP_RABBITMQ_HOST"] ?? "localhost";
var rabbitUser = builder.Configuration["CAP_RABBITMQ_USER"] ?? "guest";
var rabbitPass = builder.Configuration["CAP_RABBITMQ_PASS"] ?? "guest";

builder.Services.AddDbContext<EcommerceDbContext>(options => options.UseSqlite(sqliteConnection));
builder.Services.AddControllers();

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
    .AddSingleton<DemoOrderFactory>()
    .AddSingleton<CheckoutCliAdapter>()
    .AddSingleton<InventoryOnOrderPlacedHandler>()
    .AddSingleton<PaymentOnOrderPlacedHandler>()
    .AddSingleton<AnalyticsOnOrderPlacedHandler>()
    .AddSingleton<ShippingOnPaymentAuthorizedHandler>()
    .AddSingleton<NotifyOnPaymentAuthorizedHandler>()
    .AddSingleton<NotifyOnShipmentPreparedHandler>();

builder.Services.AddCap(capOptions =>
{
    capOptions.UseEntityFramework<EcommerceDbContext>();
    capOptions.UseSqlite(sqliteConnection);

    capOptions.UseRabbitMQ(options =>
    {
        options.HostName = rabbitHost;
        options.UserName = rabbitUser;
        options.Password = rabbitPass;
        options.VirtualHost = "/";
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

var cliAdapter = app.Services.GetRequiredService<CheckoutCliAdapter>();
await cliAdapter.RunDemoAsync();

// CAP subscribers run in background hosted services.
await Task.Delay(300);

var inventoryAdapter = app.Services.GetRequiredService<InMemoryInventoryAdapter>();
var paymentAdapter = app.Services.GetRequiredService<InMemoryPaymentAdapter>();
var shippingAdapter = app.Services.GetRequiredService<InMemoryShippingAdapter>();
var analyticsAdapter = app.Services.GetRequiredService<InMemoryAnalyticsAdapter>();
var notificationAdapter = app.Services.GetRequiredService<ConsoleNotificationAdapter>();

Console.WriteLine();
Console.WriteLine("=== Projection / Adapter State ===");
Console.WriteLine($"Reserved units: {inventoryAdapter.TotalReservedUnits}");
Console.WriteLine($"Authorized amount: ${paymentAdapter.TotalAuthorized}");
Console.WriteLine($"Shipments prepared: {shippingAdapter.ShipmentsPrepared}");
Console.WriteLine($"Orders tracked: {analyticsAdapter.OrdersTracked}");
Console.WriteLine($"Revenue tracked: ${analyticsAdapter.RevenueTracked}");
Console.WriteLine($"Notifications sent: {notificationAdapter.SentMessages}");

app.MapControllers();
await app.RunAsync();
