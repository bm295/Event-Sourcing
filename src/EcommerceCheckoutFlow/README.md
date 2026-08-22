# EcommerceCheckoutFlow (Hexagonal + Event-Driven E-Commerce + CAP)

This project demonstrates a checkout workflow built with hexagonal architecture, CAP-backed event publishing, and explicit reliability boundaries for event metadata, ordering, idempotency, and replay.

## Current Architecture

- `Domain/`
  - Core model: `Order`, `CartItem`.
  - Domain events: `OrderPlaced`, `PaymentAuthorized`, `PaymentFailed`, `OrderCancelled`, `ShipmentPrepared`.
  - `IEventEnvelope` is required for published domain events and carries `EventId`, `OccurredAt`, `CorrelationId`, `CausationId`, `EventType`, `OrderId`, and `SequenceNumber`.
  - `EventMetadata.NewRoot(...)` creates the first event in a workflow. `EventMetadata.NewChild(...)` preserves correlation, points causation at the source event, and assigns the next per-order sequence.
- `Application/`
  - `UseCases/CheckoutUseCase` is the main application input. It creates the order, allocates the next sequence number, stores the order through `IOrderStore`, and publishes `OrderPlaced` inside the transaction exposed by `ICheckoutTransactionManager`.
  - `Ports/` contains outbound dependencies for inventory, payment, shipping, notification, analytics, event publishing, message deduplication, and per-order sequence tracking.
  - `EventTopics` centralizes CAP topic names.
  - `Handlers/` contains runtime CAP subscribers that orchestrate side effects and follow-up events.
  - `Projectors/` contains pure replay/read-model code. Rebuilds go through `IReplayStateRebuilder`, `RebuildStateService`, and `CheckoutReadModelProjector`.
- `Adapters/Primary/`
  - `CheckoutCliAdapter` drives the sample checkout workflow.
  - `PublishController` exposes CAP sample routes `GET /send` and `GET /send/delay`.
- `Adapters/Secondary/`
  - In-memory adapters implement inventory, payment, shipping, analytics, and notification ports.
  - `CapEventBus` is the only adapter that calls CAP publish APIs for checkout domain events.
  - `Persistence/EcommerceDbContext` stores order records, processed-message records, order sequence state, and consumer sequence state.
  - `Persistence/EfCoreOrderStore` and `CapTransactionManager` implement the application-owned persistence and transaction ports, keeping EF Core and CAP types out of application code.

## Runtime Flow

1. A primary adapter calls `CheckoutUseCase.PlaceOrderAsync(...)`.
2. The use case creates an `OrderPlaced` event with the next sequence number from `IOrderEventSequenceAllocator`.
3. The order row and `OrderPlaced` publish are committed through the same EF/CAP transaction boundary.
4. `CapEventBus` maps the event type to a CAP topic and writes the `partitionKey` header from `OrderId`.
5. CAP subscribers validate the event through `ConsumerEventGuard`, which records the last accepted sequence for each `(ConsumerName, OrderId)`.
6. Accepted consumers call `IMessageDeduplicationStore.TryMarkProcessedAsync(...)` before side effects.
7. Secondary adapters receive stable idempotency keys such as `PaymentOnOrderPlacedHandler:{EventId}`.
8. Payment success emits `PaymentAuthorized`; payment failure emits `PaymentFailed`.
9. Shipping reacts to `PaymentAuthorized` and emits `ShipmentPrepared`.
10. Cancellation reacts to `PaymentFailed` and emits `OrderCancelled`.
11. Notification and analytics handlers react independently and never coordinate through concrete infrastructure classes.

## Reliability Model

### Storage and Transport

- CAP storage uses SQLite through `UseSqlite`.
- Checkout write persistence uses EF Core SQLite through `EcommerceDbContext`.
- RabbitMQ is the configured CAP transport.
- Business DB connection string:
  - `CHECKOUT_DB_CONNECTION` (default: `Data Source=ecommerce-checkout.db`)
- RabbitMQ settings:
  - `CAP_RABBITMQ_HOST` (default: `localhost`)
  - `CAP_RABBITMQ_USER` (default: `guest`)
  - `CAP_RABBITMQ_PASS` (default: `guest`)

### Persistence Tables

- `orders`
  - Business write model for placed orders.
- `processed_messages`
  - Consumer idempotency table keyed by `(consumer_name, event_id)`.
  - See `Adapters/Secondary/Persistence/Schema/20260514_processed_messages_retention_policy.md` for cleanup policy.
- `order_event_sequences`
  - Last allocated sequence number per order aggregate.
- `consumer_order_sequences`
  - Last accepted sequence number per consumer and order.
- CAP tables
  - Created by CAP/EF storage for local message table behavior.

## Event Envelope and Ordering

Every checkout event implements `IEventEnvelope`.

- `EventId` identifies the specific event.
- `CorrelationId` stays stable across the checkout workflow.
- `CausationId` points to the direct source event for child events.
- `OrderId` is the aggregate identity and publish partition key.
- `SequenceNumber` is strictly increasing within one order stream.

Publish rules:

- Application publish calls go through `IEventBus`, not raw `ICapPublisher`.
- `CheckoutUseCase` uses `ICheckoutTransactionManager` to open the atomic checkout boundary and does not reference EF Core or CAP transaction types.
- Domain events are published with `OrderId` partition affinity.
- `CapEventBus` sets CAP header `partitionKey=<OrderId>`.
- Follow-up events allocate a new sequence number for the same `OrderId` before publishing.

Consumer ordering rules:

- `ConsumerEventGuard` validates each event before side effects.
- `SequenceGuardDecision.Accept` allows processing.
- `SequenceGuardDecision.Duplicate` and `SequenceGuardDecision.OutOfOrder` stop processing.
- Consumers that subscribe to later events in a stream must have a sequence-guard strategy that can handle their first observed event. The current EF guard records contiguous sequence state per `(ConsumerName, OrderId)`.

## Idempotency Rules

Runtime handlers that execute side effects must follow this order:

1. Validate ordering with `ConsumerEventGuard`.
2. Call `TryMarkProcessedAsync(consumerName, eventId)` before the side effect.
3. Pass a stable idempotency key to the secondary adapter.
4. Allocate a new sequence number before publishing any follow-up domain event.
5. Publish follow-up events through `IEventBus` with `OrderId` partition affinity.

Consumer pattern:

```csharp
[CapSubscribe(EventTopics.PaymentAuthorized)]
public async Task HandleAsync(PaymentAuthorized @event)
{
    const string consumerName = nameof(ShippingOnPaymentAuthorizedHandler);

    var (_, partitionKey, decision) = await ConsumerEventGuard.ValidateAndLogAsync(
        logger,
        sequenceGuardStore,
        consumerName,
        @event);

    if (decision != SequenceGuardDecision.Accept) return;
    if (!await deduplicationStore.TryMarkProcessedAsync(consumerName, @event.EventId)) return;

    shippingPort.Prepare(@event, $"{consumerName}:{@event.EventId}");

    var seq = await sequenceAllocator.AllocateNextSequenceAsync(@event.OrderId);
    var metadata = EventMetadata.NewChild(nameof(ShipmentPrepared), @event, seq);
    var next = new ShipmentPrepared(
        metadata.EventId,
        metadata.OccurredAt,
        metadata.CorrelationId,
        metadata.CausationId,
        metadata.EventType,
        metadata.OrderId,
        metadata.SequenceNumber,
        @event.CustomerId,
        packageCount: 1);

    await eventBus.PublishAsync(next, partitionKey);
}
```

## Runtime Handlers vs Replay Boundary

`Application/Handlers/` are runtime consumers only. They may call secondary ports and publish follow-up events. Replay jobs must not execute these handlers.

Replay uses:

- `IReplayStateRebuilder`
- `RebuildStateService`
- `CheckoutReadModelProjector`
- `CheckoutReadModel`

Replay rules:

- Replay directly from an event stream into projector code.
- Do not republish historical events into CAP to rebuild read models.
- Do not reuse side-effect subscribers for replay.
- `RebuildStateService` sorts by `OrderId` and `SequenceNumber`, then validates contiguous sequence numbers per order before projection.

If replay through the broker becomes a hard requirement later, replayed messages must carry explicit replay metadata and every side-effect handler must skip replay messages before touching external systems.

## CAP Publish Samples

`PublishController` includes raw CAP sample endpoints for demonstration:

- `GET /send`
  - Calls `capBus.Publish(...)`.
- `GET /send/delay`
  - Calls `capBus.PublishDelay(TimeSpan.FromSeconds(100), ...)`.

These endpoints are CAP examples. Checkout domain publishing still goes through `IEventBus` and `CapEventBus`.

## Hexagonal Boundary Rules

- Domain and application code depend on ports and domain types, not concrete adapters.
- Raw CAP publish calls for checkout domain events are isolated to `CapEventBus`.
- Business handlers depend only on the ports they need.
- External side effects are behind secondary adapters.
- Replay/read-model rebuilds stay outside the runtime subscriber path.

## Anti-Patterns

- Publishing checkout domain events directly with `ICapPublisher` instead of `IEventBus`.
- Publishing aggregate events without `OrderId` partition affinity.
- Using random or round-robin partition keys for order events.
- Running side-effect handlers during replay.
- Calling side-effect adapters before marking the source event as processed.
- Mutating historical events instead of publishing new domain events.
