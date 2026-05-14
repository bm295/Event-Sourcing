# EcommerceCheckoutFlow Assessment

## 1) Why does each secondary adapter implement a separate interface? Can we use one/fewer interfaces?

Each secondary adapter maps to a distinct outbound port:

- `IInventoryPort`
- `IPaymentPort`
- `IShippingPort`
- `IAnalyticsPort`
- `INotificationPort`

Handlers depend only on the capability they need. This preserves interface segregation, keeps tests focused, and prevents a handler from gaining accidental access to unrelated side effects.

The application layer also has infrastructure-facing reliability ports:

- `IEventBus` for standardized domain-event publishing.
- `IMessageDeduplicationStore` for per-consumer duplicate delivery protection.
- `IOrderEventSequenceAllocator` for per-order sequence numbers.
- `IConsumerSequenceGuardStore` for per-consumer ordering checks.

You can reduce interface count only when responsibilities are genuinely coupled. A single broad interface would make handlers depend on methods they do not use and would weaken the current hexagonal boundary.

## 2) Does this project satisfy the delayed CAP publishing requirement (`capBus.PublishDelay(...)`)?

Yes.

`Adapters/Primary/PublishController.cs` exposes `GET /send/delay` and calls:

```csharp
capBus.PublishDelay(TimeSpan.FromSeconds(100), "test.show.time", DateTime.Now);
```

This endpoint is a CAP demonstration endpoint. Checkout domain events still use the application-level `IEventBus` abstraction and are published immediately through `CapEventBus`.

## 3) What are the current event-processing guardrails?

- Checkout domain events implement `IEventEnvelope`.
- `OrderId` is the aggregate identity and partition key.
- `SequenceNumber` is allocated per order through `IOrderEventSequenceAllocator`.
- `CapEventBus` maps domain events to CAP topics and sets the `partitionKey` header.
- Runtime handlers call `ConsumerEventGuard` before side effects.
- Runtime handlers call `IMessageDeduplicationStore.TryMarkProcessedAsync(...)` before side effects.
- Secondary adapters receive stable idempotency keys for side-effect calls.
- Replay uses projector code only and must not invoke CAP subscribers.

## 4) Current implementation notes

- `processed_messages` stores consumer/event deduplication state and has a documented retention policy.
- `order_event_sequences` stores the last allocated sequence number per order.
- `consumer_order_sequences` stores the last accepted sequence number per consumer/order.
- Consumers whose first observed event is not sequence `1` need an ordering strategy that accounts for that subscription boundary. The current EF guard records contiguous sequence state per `(ConsumerName, OrderId)`.
