using EcommerceCheckoutFlow.Application.UseCases;

namespace EcommerceCheckoutFlow.Adapters.Primary;

public sealed class CheckoutCliAdapter(CheckoutUseCase checkoutUseCase, DemoOrderFactory demoOrderFactory)
{
    public async Task RunDemoAsync(CancellationToken cancellationToken = default)
    {
        var orders = demoOrderFactory.CreateSampleOrders();

        foreach (var order in orders)
        {
            await checkoutUseCase.PlaceOrderAsync(
                order.OrderId,
                order.CustomerId,
                order.Items,
                cancellationToken);
        }
    }
}
