# Requirement Assessment — EcommerceCheckoutFlow

## Scope

This assessment answers:
1. Why `OrderRecord` exists inside `Adapters/Secondary/Persistence/EcommerceDbContext.cs`.
2. Whether this project satisfies the CAP requirement:
   > "You can also use CAP as an EventBus. CAP provides a simpler way to implement event publishing and subscriptions. You do not need to inherit or implement any interface during subscription and sending process."
3. Whether this project satisfies the CAP startup guide:
   > In `Startup.cs`, configure CAP with `UseInMemoryStorage()` and `UseInMemoryMessageQueue()`.

4. Whether this project satisfies the CAP publishing sample:
   > `PublishController` with `/send` route and `capBus.Publish("test.show.time", DateTime.Now)`.
5. Why `CheckoutCliAdapter.cs` contains many magic values and how to keep clean code.

---

## 1) Why `OrderRecord` is included in `EcommerceDbContext.cs`

### Finding
`OrderRecord` is the **persistence model** for EF Core, used to map the domain `Order` aggregate into the relational `orders` table.

### Evidence
- `EcommerceDbContext` exposes `DbSet<OrderRecord> Orders` and configures table/column mapping in `OnModelCreating`.
- `OrderRecord.From(Order order)` transforms a domain object into DB-storable data (including JSON serialization of line items).
- `CheckoutUseCase` writes `OrderRecord` through `dbContext.Orders.Add(OrderRecord.From(order))` before saving and publishing.

### Assessment
Keeping `OrderRecord` near `EcommerceDbContext` is intentional in a hexagonal architecture: it keeps storage shape concerns in the secondary adapter layer instead of leaking EF-specific schema details into the domain model.

---

## 2) CAP EventBus requirement compliance

### Requirement interpretation
The requirement has two practical parts:
1. CAP should be usable as event bus for publishing/subscribing.
2. Subscription and sending should not require inheriting/implementing CAP-specific interfaces.

### Finding
**Status: Partially satisfied (with one architectural caveat).**

#### What is satisfied
- **Subscription style matches CAP simplicity**:
  - Handlers use `[CapSubscribe("topic")]` methods.
  - Handler classes do **not** implement CAP subscriber interfaces.
- **Event sending with CAP works without CAP interface inheritance on event types**:
  - Publishing uses `ICapPublisher.PublishAsync(...)` under the hood.
  - Event classes are plain records/types implementing only the project’s own `IDomainEvent`, not CAP contracts.

#### Caveat against strict wording
- The project introduces its own `IEventBus` abstraction (`Application/Ports/IEventBus.cs`) and `CapEventBus` adapter implementing that abstraction.
- Therefore, while CAP itself does not force interface inheritance for publish/subscribe, this codebase still requires **application-level interface implementation** (`IEventBus`) as an architectural choice.

### Conclusion
- If interpreted as a **CAP feature requirement** (no CAP interface inheritance needed): ✅ met.
- If interpreted as a **project-level requirement** (no interfaces at all in send/subscribe path): ⚠️ not fully met, because `IEventBus` is still implemented intentionally for hexagonal decoupling.

---

## 3) CAP startup guide compliance (`UseInMemoryStorage` + `UseInMemoryMessageQueue`)

### Guide being checked
```csharp
services.AddCap(x =>
{
    x.UseInMemoryStorage();
    x.UseInMemoryMessageQueue();
});
```

### Finding
**Status: Not satisfied.**

### Evidence
- This project is a minimal-host app in `Program.cs` (no `Startup.cs`), which is acceptable in modern .NET.
- CAP is configured with persistent components instead:
  - `capOptions.UseEntityFramework<EcommerceDbContext>();`
  - `capOptions.UseSqlite(sqliteConnection);`
  - `capOptions.UseRabbitMQ(...)`
- There is no `UseInMemoryStorage()` or `UseInMemoryMessageQueue()` call.

### Conclusion
Against this specific guide, the answer is **No**.
The project intentionally uses durable SQL + RabbitMQ integration rather than in-memory storage/queue.

---

## Recommendation
- If your target is to match that exact sample guide, replace current CAP setup with in-memory options.
- If your target is reliability and real integration behavior, keep current durable setup (SQL + RabbitMQ), because in-memory storage/queue is typically only suitable for demos/tests.


---

## 4) CAP publishing sample compliance (`PublishController` + `capBus.Publish(...)`)

### Guide being checked
```csharp
public class PublishController : Controller
{
    [Route("~/send")]
    public IActionResult SendMessage([FromServices] ICapPublisher capBus)
    {
        capBus.Publish("test.show.time", DateTime.Now);

        return Ok();
    }
}
```

### Finding
**Status: Partially satisfied.**

### Evidence
- This project is a console/minimal-host flow, not an ASP.NET MVC app; there is no `Controller` or `/send` HTTP endpoint.
- Message publishing with CAP does exist:
  - `CheckoutUseCase` injects `ICapPublisher`.
  - It publishes using `await capPublisher.PublishAsync(EventTopics.OrderPlaced, orderPlaced, ...)`.

### Conclusion
- **Exact sample parity (controller route + `Publish`)**: ❌ not satisfied.
- **Core capability (publishing a message via `ICapPublisher`)**: ✅ satisfied, implemented with async API and domain topic/event payload.



---

## 5) `CheckoutCliAdapter.cs` magic values and clean-code direction

### Finding
The previous inline demo literals were refactored. `CheckoutCliAdapter` is now orchestration-only and no longer owns hard-coded order/item data.

### Evidence
- Adapter now loops through orders returned by `DemoOrderFactory.CreateSampleOrders()` and calls the use case.
- New dedicated data source/factory file (`CheckoutDemoOrders.cs`) contains named constants, default sample orders, configuration loading, and validation.

### Assessment against requested implementation
1. **Dedicated demo data source:** ✅ Implemented via `CheckoutDemoOrders` + `DemoOrderFactory`.
2. **Replace inline literals with named constants/value objects:** ✅ Implemented via constants in `CheckoutDemoOrders` and `DemoCheckoutOrder` record.
3. **Move sample order construction into factory:** ✅ Implemented via `DemoOrderFactory.CreateSampleOrders()`.
4. **Support evolution beyond demo with configuration + validation:** ✅ Implemented baseline support by reading `CheckoutDemo:Orders` from configuration and validating data (including domain-level validation through `Order.Create(...)`) before invoking use case.

### Conclusion
This area now follows cleaner adapter boundaries: orchestration in `CheckoutCliAdapter`, scenario construction/config parsing in the factory, and reusable named values in a dedicated demo data source.
