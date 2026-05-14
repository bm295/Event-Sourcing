# EcommerceCheckoutFlow (Hexagonal + Event-Driven E-Commerce + CAP)

This project demonstrates how to apply **hexagonal architecture (ports and adapters)** together with an **event-driven workflow** in an e-commerce checkout domain, using **[CAP](https://cap.dotnetcore.xyz/)** as the event bus and local message table implementation.

## Structure

- `Domain/`
  - Core business model and domain events (`Order`, `CartItem`, `OrderPlaced`, `PaymentAuthorized`, `PaymentFailed`, `OrderCancelled`, `ShipmentPrepared`).
- `Application/`
  - `UseCases/CheckoutUseCase` as the main application input.
  - `Ports/` for outbound dependencies (`IInventoryPort`, `IPaymentPort`, `IShippingPort`, `INotificationPort`, `IAnalyticsPort`, `IEventBus`).
  - `EventTopics` for CAP topic names.
  - `Handlers/` for event-driven orchestration logic.
- `Adapters/Primary/`
  - `CheckoutCliAdapter` as the driving adapter (entry point interaction).
  - `PublishController` exposes HTTP routes `~/send` and `~/send/delay` for CAP publish examples.
- `Adapters/Secondary/`
  - In-memory implementations for inventory, payment, shipping, analytics, notifications.
  - `CapEventBus` implementation (`IEventBus`) to publish domain events with CAP.
  - `Persistence/EcommerceDbContext` for order write persistence.

## Reliability configuration (CAP local message table)

- CAP storage is backed by SQLite (`UseSqlite`) instead of in-memory storage.
- Checkout writes business data (`orders` table) and publishes `OrderPlaced` within the same CAP transaction boundary (`BeginTransaction(capPublisher, ...)`).
- Transport is RabbitMQ (`UseRabbitMQ`) and can be configured via env vars:
  - `CAP_RABBITMQ_HOST` (default `localhost`)
  - `CAP_RABBITMQ_USER` (default `guest`)
  - `CAP_RABBITMQ_PASS` (default `guest`)
- Business DB connection is configurable via `CHECKOUT_DB_CONNECTION` (default `Data Source=ecommerce-checkout.db`).

## Event flow

1. Primary adapter calls `CheckoutUseCase.PlaceOrderAsync(...)`.
2. Use case stores order data and publishes `OrderPlaced` in one CAP transaction.
3. CAP subscribers (`[CapSubscribe]`) react independently:
   - inventory reservation,
   - payment authorization (publishes `PaymentAuthorized` on success or `PaymentFailed` on error),
   - analytics tracking.
4. On `PaymentFailed`, the workflow emits `OrderCancelled` as a follow-up business event (without mutating prior events).
5. Shipping handler reacts to `PaymentAuthorized`, prepares shipment, then publishes `ShipmentPrepared`.
6. Notification handlers react to payment, cancellation, and shipment events.

## Why this is hexagonal

- Domain and use cases depend on **ports**, not concrete infrastructure.
- Adapters implement ports and can be replaced (DB, message broker, payment provider, etc.) without changing domain/application rules.
- Event handlers keep cross-component coordination decoupled and extensible.

## Idempotency key convention

- Secondary ports for inventory/payment/shipping/notification now receive `idempotencyKey`.
- Prefer using upstream `EventId` as the stable source identity.
- Recommended key formats:
  - `ConsumerName:EventId`
  - `Operation:OrderId:EventType`
- Adapters keep an in-memory processed-key log and skip duplicate keys.
- For follow-up publishes, handlers derive a stable dedup key from the source event to prevent duplicate event chains.

## Consumer Safety Checklist

Use this checklist for every CAP consumer that executes side effects.

- [ ] Call `TryMarkProcessedAsync` **before** any side effect.
- [ ] Use a stable side-effect idempotency key (recommended: `${ConsumerName}:${EventId}`).
- [ ] If the handler publishes follow-up events, use a deterministic dedupe key for that publish path (for example `DeterministicGuid.FromSource(eventId, nameof(FollowUpEvent))`).
- [ ] Ensure replay/rebuild path does **not** run through subscriber runtime (`[CapSubscribe]` handlers are runtime-only).

### Consumer chuẩn (copy/paste pattern)

Example below is adapted from `ShippingOnPaymentAuthorizedHandler`:

```csharp
[CapSubscribe(EventTopics.PaymentAuthorized)]
public async Task HandleAsync(PaymentAuthorized @event)
{
    const string consumerName = nameof(ShippingOnPaymentAuthorizedHandler);
    var eventId = @event.EventId;

    // 1) Guard duplicate delivery trước side effect.
    if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, eventId))
    {
        return;
    }

    // 2) Side effect với idempotency key ổn định.
    shippingPort.Prepare(@event, $"{consumerName}:{eventId}");

    // 3) Follow-up publish với deterministic dedupe key.
    if (!await deduplicationStore.TryMarkProcessedAsync(
            consumerName,
            DeterministicGuid.FromSource(eventId, nameof(ShipmentPrepared))))
    {
        return;
    }

    var metadata = EventMetadata.NewChild(nameof(ShipmentPrepared), @event);
    var shipmentPrepared = new ShipmentPrepared(
        metadata.EventId,
        metadata.OccurredAt,
        metadata.CorrelationId,
        metadata.CausationId,
        metadata.EventType,
        metadata.OrderId,
        @event.CustomerId,
        packageCount: 1);

    await eventBus.PublishAsync(shipmentPrepared);
}
```

## Runtime handlers vs projection/rebuild boundary

- `Application/Handlers/` are **runtime consumers only**. They orchestrate outbound side effects via ports (payment, inventory, shipping, notification, analytics) and may publish follow-up events through CAP.
- Replay/rebuild jobs must **not** execute these handlers, otherwise external effects can run again.
- `Application/Projectors/` contains pure read-model projection components:
  - `CheckoutReadModelProjector` applies events to `CheckoutReadModel` only.
  - `RebuildStateService` implements `IReplayStateRebuilder` and reads an event stream to replay through projector logic only.
- All replay jobs must depend on `IReplayStateRebuilder` and therefore replay via `RebuildStateService` + `CheckoutReadModelProjector`.

### Replay boundary checklist (Do / Don’t)

- ✅ **Do**
  - Replay trực tiếp event stream vào projector (`IReplayStateRebuilder` -> `RebuildStateService` -> `CheckoutReadModelProjector`).
  - Keep replay logic outside CAP subscriber execution path.
- ❌ **Don’t**
  - Publish lại historical events vào CAP để rebuild read model.
  - Reuse runtime side-effect subscribers for replay.

If replay-via-bus becomes mandatory in the future, every replayed message must include metadata flag `IsReplay=true`, and all side-effect handlers must skip processing when this flag is enabled.

## Ordering Guarantee by Aggregate

This service enforces **per-aggregate ordering** by publishing every domain event with:

- `PartitionKey = OrderId`
- CAP message header `partitionKey=<OrderId>` (set in `CapEventBus`)
- RabbitMQ logical stream convention: `routing-key = order.{OrderId}` for consumer binding policies (single convention for aggregate stream).

Code pattern:

```csharp
public static string GetPartitionKey(this IEventEnvelope @event) => @event.OrderId;

await eventBus.PublishAsync(@event, @event.GetPartitionKey(), cancellationToken);
```

### Kafka option

If switching CAP transport to Kafka, configure producer message key from the same logical key:

- Option name: `Kafka:UsePartitionKeyAsMessageKey` (application-level convention)
- Place to configure: app configuration + `CapEventBus` broker adapter mapping.
- Example:

```json
{
  "Kafka": {
    "UsePartitionKeyAsMessageKey": true
  }
}
```

When enabled, `message.key = OrderId`, ensuring all events from one order stay in one partition.

### Anti-patterns (forbidden)

- Random partition key for aggregate domain events.
- Round-robin publish for aggregate domain events.
- Publishing without `OrderId`/partition metadata.
