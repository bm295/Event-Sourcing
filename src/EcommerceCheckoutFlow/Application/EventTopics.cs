namespace EcommerceCheckoutFlow.Application;

public static class EventTopics
{
    public const string OrderPlaced = "checkout.order.placed";
    public const string PaymentAuthorized = "checkout.payment.authorized";
    public const string ShipmentPrepared = "checkout.shipment.prepared";
}
