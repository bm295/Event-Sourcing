using EcommerceCheckoutFlow.Domain;

namespace EcommerceCheckoutFlow.Application.Ports;

public interface IOrderStore
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
}
