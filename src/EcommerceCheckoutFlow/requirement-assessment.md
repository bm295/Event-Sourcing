# EcommerceCheckoutFlow Assessment

## 1) Why does each secondary adapter implement a separate interface? Can we use one/fewer interfaces?

Each adapter currently maps to a distinct outbound port (`IInventoryPort`, `IPaymentPort`, `IShippingPort`, `IAnalyticsPort`, `INotificationPort`) so handlers depend only on the capability they need. This preserves interface segregation and keeps business handlers decoupled from unrelated methods.

You *can* reduce interface count, but with trade-offs:
- One “god” interface would force handlers to depend on methods they do not use (worse cohesion, harder testing/mocking).
- A smaller grouped set can work only when responsibilities are truly coupled.

For this codebase, separate interfaces are the cleaner hexagonal design choice.

## 2) Does this project satisfy the delayed CAP publishing requirement (`capBus.PublishDelay(...)`)?

**No (not currently).**

The project publishes events with `PublishAsync(...)` and uses `[CapSubscribe]` handlers, but there is no delayed publish call (`PublishDelay(...)`) and no endpoint equivalent to `/send/delay` shown in the requirement snippet.
