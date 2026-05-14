# ADR 0003: E-commerce checkout event processing boundaries

## Status
Accepted - 2026-05-14

## Context

`EcommerceCheckoutFlow` uses CAP to publish checkout domain events and run subscriber handlers. The workflow performs external side effects such as inventory reservation, payment authorization, shipment preparation, notification, and analytics. Duplicate broker delivery, replay, and cross-order interleaving must not duplicate side effects or break per-order event ordering.

## Decision

- Checkout domain events must implement `IEventEnvelope`.
- `OrderId` is the aggregate identity and the publish partition key.
- `SequenceNumber` is allocated per order through `IOrderEventSequenceAllocator`.
- Application publish calls for checkout domain events go through `IEventBus`.
- Application code may use `ICapPublisher` to open the EF/CAP transaction boundary, but not to publish checkout domain events directly.
- Raw CAP publish calls for checkout domain events are isolated to `CapEventBus`.
- `CapEventBus` maps domain event types to CAP topics and sets `partitionKey=<OrderId>`.
- Runtime subscribers must validate ordering with `ConsumerEventGuard` before side effects.
- Runtime subscribers must call `IMessageDeduplicationStore.TryMarkProcessedAsync(consumerName, eventId)` before side effects.
- Secondary adapters that can cause side effects must receive stable idempotency keys.
- Replay/rebuild jobs must use projector code only and must not execute CAP subscriber handlers.
- Delayed CAP publishing is exposed only as a sample endpoint in `PublishController`; checkout domain workflow events remain immediate publishes through `IEventBus`.

## Persistence

- `orders` stores the checkout write model.
- `processed_messages` stores consumer/event deduplication records.
- `order_event_sequences` stores the last allocated sequence number per order.
- `consumer_order_sequences` stores the last accepted sequence number per consumer and order.
- CAP storage provides the local message table for broker publishing.

## Consequences

- Event publishing has one application abstraction and one CAP adapter boundary.
- Per-order ordering is explicit in event metadata and broker partition headers.
- Duplicate delivery is handled before side effects.
- Replay stays deterministic and isolated from external systems.
- Consumers whose first observed event is not sequence `1` need an ordering strategy that accounts for their subscription boundary, because the current sequence guard records contiguous sequence state per `(ConsumerName, OrderId)`.
