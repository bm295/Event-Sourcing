using Microsoft.EntityFrameworkCore;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

// EnsureCreated does not add tables to an existing demo database. This additive,
// idempotent upgrade also supports databases created before the payment journal.
public static class PaymentProcessingSchema
{
    public static Task EnsureCreatedAsync(EcommerceDbContext dbContext) =>
        dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS payment_processing (
                InputEventId TEXT NOT NULL PRIMARY KEY,
                OutcomeType TEXT NOT NULL,
                OutcomeJson TEXT NOT NULL,
                Completed INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS payment_authorizations (
                IdempotencyKey TEXT NOT NULL PRIMARY KEY,
                OrderId TEXT NOT NULL,
                Amount TEXT NOT NULL
            );
            """);
}
