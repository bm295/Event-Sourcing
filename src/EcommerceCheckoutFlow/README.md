# EcommerceCheckoutFlow (Hexagonal + Event-Driven E-Commerce + CAP)

This project demonstrates how to apply **hexagonal architecture (ports and adapters)** together with an **event-driven workflow** in an e-commerce checkout domain, using **[CAP](https://cap.dotnetcore.xyz/)** as the event bus and local message table implementation.

## Structure

- `Domain/`
  - Core business model and domain events (`Order`, `CartItem`, `OrderPlaced`, `PaymentAuthorized`, `ShipmentPrepared`).
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
   - payment authorization (then publishes `PaymentAuthorized`),
   - analytics tracking.
4. Shipping handler reacts to `PaymentAuthorized`, prepares shipment, then publishes `ShipmentPrepared`.
5. Notification handlers react to payment and shipment events.

## Why this is hexagonal

- Domain and use cases depend on **ports**, not concrete infrastructure.
- Adapters implement ports and can be replaced (DB, message broker, payment provider, etc.) without changing domain/application rules.
- Event handlers keep cross-component coordination decoupled and extensible.
