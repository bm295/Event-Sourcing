using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Projectors;

public sealed class CheckoutReadModelProjector
{
    public void Project(CheckoutReadModel readModel, IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderPlaced orderPlaced:
                readModel.Apply(orderPlaced);
                break;
            case PaymentAuthorized paymentAuthorized:
                readModel.Apply(paymentAuthorized);
                break;
            case PaymentFailed paymentFailed:
                readModel.Apply(paymentFailed);
                break;
            case OrderCancelled orderCancelled:
                readModel.Apply(orderCancelled);
                break;
            case ShipmentPrepared shipmentPrepared:
                readModel.Apply(shipmentPrepared);
                break;
            default:
                throw new NotSupportedException($"Unsupported event type for projector: {@event.GetType().Name}");
        }
    }
}
