# EcommerceCheckoutFlow (Hexagonal + Event-Driven E-Commerce)

This project demonstrates how to apply **hexagonal architecture (ports and adapters)** together with an **event-driven workflow** in an e-commerce checkout domain.

## Structure

- `Domain/`
  - Core business model and domain events (`Order`, `CartItem`, `OrderPlaced`, `PaymentAuthorized`, `ShipmentPrepared`).
- `Application/`
  - `UseCases/CheckoutUseCase` as the main application input.
  - `Ports/` for outbound dependencies (`IInventoryPort`, `IPaymentPort`, `IShippingPort`, `INotificationPort`, `IAnalyticsPort`, `IEventBus`).
  - `Handlers/` for event-driven orchestration logic.
- `Adapters/Primary/`
  - `CheckoutCliAdapter` as the driving adapter (entry point interaction).
- `Adapters/Secondary/`
  - In-memory implementations for inventory, payment, shipping, analytics, notifications, and event bus.

## Event flow

1. Primary adapter calls `CheckoutUseCase.PlaceOrder(...)`.
2. Use case publishes `OrderPlaced` through `IEventBus`.
3. Application handlers react independently:
   - inventory reservation,
   - payment authorization (then publishes `PaymentAuthorized`),
   - analytics tracking.
4. Shipping handler reacts to `PaymentAuthorized`, prepares shipment, then publishes `ShipmentPrepared`.
5. Notification handlers react to payment and shipment events.

## Why this is hexagonal

- Domain and use cases depend on **ports**, not concrete infrastructure.
- Adapters implement ports and can be replaced (DB, message broker, payment provider, etc.) without changing domain/application rules.
- Event handlers keep cross-component coordination decoupled and extensible.
