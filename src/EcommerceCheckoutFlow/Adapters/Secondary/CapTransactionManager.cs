using DotNetCore.CAP;
using EcommerceCheckoutFlow.Adapters.Secondary.Persistence;
using EcommerceCheckoutFlow.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EcommerceCheckoutFlow.Adapters.Secondary;

public sealed class CapTransactionManager(ICapPublisher capPublisher, EcommerceDbContext dbContext) : ICheckoutTransactionManager
{
    public ICheckoutTransaction Begin()
        => new CapCheckoutTransaction(dbContext.Database.BeginTransaction(capPublisher, autoCommit: false));

    private sealed class CapCheckoutTransaction(IDbContextTransaction transaction) : ICheckoutTransaction
    {
        public void Commit() => transaction.Commit();

        public void Dispose() => transaction.Dispose();
    }
}
