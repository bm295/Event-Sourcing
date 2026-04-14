using EcommerceCheckoutFlow.Application.UseCases;
using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Adapters.Primary;

public sealed class CheckoutCliAdapter(CheckoutUseCase checkoutUseCase)
{
    public async Task RunDemoAsync(CancellationToken cancellationToken = default)
    {
        await checkoutUseCase.PlaceOrderAsync(
            orderId: "ORD-3001",
            customerId: "CUS-501",
            items:
            [
                new CartItem("SKU-MOUSE", "Wireless Mouse", 2, 25m),
                new CartItem("SKU-USB", "USB-C Cable", 1, 12m)
            ],
            cancellationToken);

        await checkoutUseCase.PlaceOrderAsync(
            orderId: "ORD-3002",
            customerId: "CUS-502",
            items:
            [
                new CartItem("SKU-KEYBOARD", "Mechanical Keyboard", 1, 120m)
            ],
            cancellationToken);
    }
}
