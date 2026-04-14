# EcommerceCheckoutFlow (Hexagonal + Event-Driven E-Commerce + CAP)

This project demonstrates how to apply **hexagonal architecture (ports and adapters)** together with an **event-driven workflow** in an e-commerce checkout domain, using **[CAP](https://cap.dotnetcore.xyz/)** as the event bus.

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
- `Adapters/Secondary/`
  - In-memory implementations for inventory, payment, shipping, analytics, notifications.
  - `CapEventBus` implementation (`IEventBus`) to publish domain events with CAP.

## Event flow

1. Primary adapter calls `CheckoutUseCase.PlaceOrderAsync(...)`.
2. Use case publishes `OrderPlaced` through `IEventBus` (`CapEventBus` → CAP topic).
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
- CAP allows replacing in-memory queue/storage with real broker/storage providers later (Kafka, RabbitMQ, SQL, etc.).
