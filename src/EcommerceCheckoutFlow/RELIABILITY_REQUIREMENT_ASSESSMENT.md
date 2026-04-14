# Reliability Requirement Assessment (CAP Local Message Table)

## Requirement checked

> In SOA/MicroService integration, message queues alone are not enough for reliability. CAP should use a **local message table integrated with the current business database** so distributed-call exceptions do not lose events.

## Re-verification verdict

**Status: Satisfied by current `EcommerceCheckoutFlow` implementation (with infrastructure prerequisites).**

## Evidence from implementation

1. CAP now uses durable relational storage:
   - `capOptions.UseSqlite(sqliteConnection);`
   - `capOptions.UseEntityFramework<EcommerceDbContext>();`
2. CAP transport is a broker transport (RabbitMQ), not in-memory queue:
   - `capOptions.UseRabbitMQ(...)`
3. Business write + publish are in the same CAP transaction boundary in checkout flow:
   - `using var transaction = dbContext.Database.BeginTransaction(capPublisher, autoCommit: false);`
   - `dbContext.Orders.Add(...); await dbContext.SaveChangesAsync(...);`
   - `await capPublisher.PublishAsync(EventTopics.OrderPlaced, ...);`
   - `transaction.Commit();`
4. Business data persistence exists in `EcommerceDbContext` with an `orders` table (`OrderRecord`) used by checkout use case.

## Requirement-by-requirement check

1. **Durable CAP storage provider backed by service DB:** ✅ Met (`UseSqlite` + `EcommerceDbContext`).
2. **Persist business data and publish in same transaction boundary:** ✅ Met (`BeginTransaction(capPublisher, ...)` around save + publish).
3. **Use a transport such as RabbitMQ/Kafka with durable storage consistency:** ✅ Met (`UseRabbitMQ` transport and SQL-backed CAP storage).

## Operational prerequisites / caveat

To realize end-to-end reliability in runtime, infrastructure must be available:
- RabbitMQ reachable at configured host/user/password.
- SQLite file path writable by the process (or replace with production DB provider).

If those dependencies are unavailable at runtime, message processing can fail despite correct code-level integration.
