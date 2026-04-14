using System.Globalization;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Extensions.Configuration;

namespace EcommerceCheckoutFlow.Adapters.Primary;

public sealed record DemoCheckoutOrder(
    string OrderId,
    string CustomerId,
    IReadOnlyList<CartItem> Items);

public static class CheckoutDemoOrders
{
    public const string DefaultOrderId1 = "ORD-3001";
    public const string DefaultCustomerId1 = "CUS-501";
    public const string DefaultOrderId2 = "ORD-3002";
    public const string DefaultCustomerId2 = "CUS-502";

    public const string MouseSku = "SKU-MOUSE";
    public const string MouseName = "Wireless Mouse";
    public const int MouseQuantity = 2;
    public const decimal MouseUnitPrice = 25m;

    public const string UsbCableSku = "SKU-USB";
    public const string UsbCableName = "USB-C Cable";
    public const int UsbCableQuantity = 1;
    public const decimal UsbCableUnitPrice = 12m;

    public const string KeyboardSku = "SKU-KEYBOARD";
    public const string KeyboardName = "Mechanical Keyboard";
    public const int KeyboardQuantity = 1;
    public const decimal KeyboardUnitPrice = 120m;

    public static IReadOnlyList<DemoCheckoutOrder> Defaults =>
    [
        new DemoCheckoutOrder(
            DefaultOrderId1,
            DefaultCustomerId1,
            [
                new CartItem(MouseSku, MouseName, MouseQuantity, MouseUnitPrice),
                new CartItem(UsbCableSku, UsbCableName, UsbCableQuantity, UsbCableUnitPrice)
            ]),
        new DemoCheckoutOrder(
            DefaultOrderId2,
            DefaultCustomerId2,
            [
                new CartItem(KeyboardSku, KeyboardName, KeyboardQuantity, KeyboardUnitPrice)
            ])
    ];
}

public sealed class DemoOrderFactory(IConfiguration configuration)
{
    public IReadOnlyList<DemoCheckoutOrder> CreateSampleOrders()
    {
        var configuredOrders = LoadConfiguredOrders();
        var orders = configuredOrders.Count == 0 ? CheckoutDemoOrders.Defaults : configuredOrders;

        ValidateOrders(orders);
        return orders;
    }

    private List<DemoCheckoutOrder> LoadConfiguredOrders()
    {
        var ordersSection = configuration.GetSection("CheckoutDemo:Orders");
        if (!ordersSection.Exists())
        {
            return [];
        }

        var configuredOrders = new List<DemoCheckoutOrder>();

        foreach (var orderSection in ordersSection.GetChildren())
        {
            var orderId = orderSection["OrderId"] ?? string.Empty;
            var customerId = orderSection["CustomerId"] ?? string.Empty;

            var items = new List<CartItem>();
            foreach (var itemSection in orderSection.GetSection("Items").GetChildren())
            {
                var sku = itemSection["Sku"] ?? string.Empty;
                var name = itemSection["Name"] ?? string.Empty;
                var quantity = ParseInt(itemSection["Quantity"], "Quantity");
                var unitPrice = ParseDecimal(itemSection["UnitPrice"], "UnitPrice");

                items.Add(new CartItem(sku, name, quantity, unitPrice));
            }

            configuredOrders.Add(new DemoCheckoutOrder(orderId, customerId, items));
        }

        return configuredOrders;
    }

    private static int ParseInt(string? value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Invalid CheckoutDemo configuration: '{fieldName}' must be an integer.");
        }

        return parsed;
    }

    private static decimal ParseDecimal(string? value, string fieldName)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Invalid CheckoutDemo configuration: '{fieldName}' must be a decimal.");
        }

        return parsed;
    }

    private static void ValidateOrders(IReadOnlyList<DemoCheckoutOrder> orders)
    {
        if (orders.Count == 0)
        {
            throw new InvalidOperationException("At least one checkout demo order is required.");
        }

        foreach (var order in orders)
        {
            if (string.IsNullOrWhiteSpace(order.OrderId))
            {
                throw new InvalidOperationException("Checkout demo order id is required.");
            }

            if (string.IsNullOrWhiteSpace(order.CustomerId))
            {
                throw new InvalidOperationException($"Checkout demo customer id is required for order '{order.OrderId}'.");
            }

            foreach (var item in order.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Sku))
                {
                    throw new InvalidOperationException($"Checkout demo SKU is required for order '{order.OrderId}'.");
                }

                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    throw new InvalidOperationException($"Checkout demo item name is required for order '{order.OrderId}'.");
                }

                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException($"Checkout demo item quantity must be > 0 for order '{order.OrderId}'.");
                }

                if (item.UnitPrice <= 0)
                {
                    throw new InvalidOperationException($"Checkout demo item unit price must be > 0 for order '{order.OrderId}'.");
                }
            }

            // Domain-level validation before invoking use case.
            _ = Order.Create(order.OrderId, order.CustomerId, order.Items);
        }
    }
}
