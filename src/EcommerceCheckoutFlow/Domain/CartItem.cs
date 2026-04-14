namespace EcommerceCheckoutFlow.Domain;

public sealed record CartItem(string Sku, string Name, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}
