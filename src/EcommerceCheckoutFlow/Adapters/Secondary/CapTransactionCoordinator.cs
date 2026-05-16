using DotNetCore.CAP;
using EcommerceCheckoutFlow.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EcommerceCheckoutFlow.Adapters.Secondary;

public sealed class CapTransactionCoordinator(ICapPublisher capPublisher) : ICapTransactionCoordinator
{
    public IDbContextTransaction BeginTransaction(DbContext dbContext, bool autoCommit = false)
        => dbContext.Database.BeginTransaction(capPublisher, autoCommit);
}
