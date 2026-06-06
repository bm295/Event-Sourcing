# OrderPlacedHandlers Documentation

## Overview
`OrderPlacedHandlers.cs` contains three event handlers that respond to `OrderPlaced` domain events in the e-commerce checkout flow. Each handler is an independent CAP (Consistent and Partition-tolerant) subscriber that performs a specific side effect when an order is placed.

## File Location
- **Path:** `src/EcommerceCheckoutFlow/Application/Handlers/OrderPlacedHandlers.cs`

## Architecture Pattern
- **Pattern:** Hexagonal/Ports & Adapters + Event-Driven Architecture
- **Message Broker:** RabbitMQ via DotNetCore.CAP
- **Subscription Model:** CAP subscribers (`[CapSubscribe(EventTopics.OrderPlaced)]`)
- **Concurrency Guards:** Event sequence tracking + message deduplication

---

## Handler Classes

### 1. `InventoryOnOrderPlacedHandler`

#### Purpose
Reserves inventory items when an order is placed.

#### Flow
```
OrderPlaced event received
  ↓
GuardAndDeduplicateAsync()
  ├─ ValidateAndLogAsync() → Check sequence number
  ├─ SequenceGuardDecision.Accept check → Return early if not accepted
  ├─ TryMarkProcessedAsync() → Mark event as processed (idempotency)
  └─ Return ConsumerEventValidationResult
  ↓
inventoryPort.ReserveItems(@event, idempotencyKey)
```

#### Dependencies (Callee Calls)
- `IInventoryPort.ReserveItems()`
  - **Implementation:** `InMemoryInventoryAdapter` (configured in `Program.cs`)
  - **Effect:** Reserves items in in-memory inventory store
  - **Idempotency Key:** `"{consumerName}:{@event.EventId}"` → `"InventoryOnOrderPlacedHandler:{eventId}"`

- `OrderPlacedHandlersHelper.GuardAndDeduplicateAsync()`
  - Validates event sequence order
  - Marks event as processed to prevent duplicates

#### Callers
- **DotNetCore.CAP Framework** (via `[CapSubscribe(EventTopics.OrderPlaced)]`)
  - CAP automatically discovers and registers this handler at application startup
  - Invoked when `OrderPlaced` event is published to RabbitMQ

#### Registration
```csharp
// Program.cs line 48
.AddSingleton<InventoryOnOrderPlacedHandler>()
```

---

### 2. `PaymentOnOrderPlacedHandler`

#### Purpose
Authorizes payment and publishes follow-up events (`PaymentAuthorized` or `PaymentFailed`).

#### Flow
```
OrderPlaced event received
  ↓
GuardAndDeduplicateAsync()
  ├─ ValidateAndLogAsync() → Check sequence number
  ├─ SequenceGuardDecision.Accept check → Return early if not accepted
  ├─ TryMarkProcessedAsync() → Mark event as processed (idempotency)
  └─ Return ConsumerEventValidationResult with partitionKey
  ↓
Extract partitionKey from validation result
  ↓
try
{
  paymentPort.Authorize(@event, idempotencyKey)
    ↓
  sequenceAllocator.AllocateNextSequenceAsync(@event.OrderId)
    ↓
  EventMetadata.NewChild() → Create child event metadata
    ↓
  new PaymentAuthorized(...) → Create success event
    ↓
  eventBus.PublishAsync(next, partitionKey) ✓
}
catch (Exception ex)
{
  sequenceAllocator.AllocateNextSequenceAsync(@event.OrderId)
    ↓
  EventMetadata.NewChild() → Create child event metadata
    ↓
  new PaymentFailed(...) → Create failure event
    ↓
  eventBus.PublishAsync(failed, partitionKey) ✗
}
```

#### Dependencies (Callee Calls)

- `IPaymentPort.Authorize()`
  - **Implementation:** `InMemoryPaymentAdapter`
  - **Effect:** Authorizes payment for the order amount
  - **Idempotency Key:** `"{consumerName}:{@event.EventId}"`
  - **May Throw:** Exception triggers failure path

- `IOrderEventSequenceAllocator.AllocateNextSequenceAsync()`
  - **Implementation:** `EfCoreOrderEventSequenceAllocator`
  - **Effect:** Allocates the next sequence number for the order's event stream
  - **Used:** For both success and failure event publication

- `EventMetadata.NewChild()`
  - Creates event metadata with proper causal relationships
  - Links causation to the original `OrderPlaced` event

- `IEventBus.PublishAsync()`
  - **Implementation:** `CapEventBus` (adapts to RabbitMQ via CAP)
  - **Success Path:** Publishes `PaymentAuthorized` event with partition key
  - **Failure Path:** Publishes `PaymentFailed` event with partition key

- `OrderPlacedHandlersHelper.GuardAndDeduplicateAsync()`
  - Validates event sequence order
  - Marks event as processed to prevent duplicates

#### Callers
- **DotNetCore.CAP Framework** (via `[CapSubscribe(EventTopics.OrderPlaced)]`)
  - CAP automatically discovers and registers this handler at application startup
  - Invoked when `OrderPlaced` event is published to RabbitMQ

#### Published Events
- **Success:** `PaymentAuthorized` event → triggers downstream handlers
- **Failure:** `PaymentFailed` event → triggers order cancellation

#### Registration
```csharp
// Program.cs line 49
.AddSingleton<PaymentOnOrderPlacedHandler>()
```

---

### 3. `AnalyticsOnOrderPlacedHandler`

#### Purpose
Tracks order placement events for analytics/reporting.

#### Flow
```
OrderPlaced event received
  ↓
GuardAndDeduplicateAsync()
  ├─ ValidateAndLogAsync() → Check sequence number
  ├─ SequenceGuardDecision.Accept check → Return early if not accepted
  ├─ TryMarkProcessedAsync() → Mark event as processed (idempotency)
  └─ Return ConsumerEventValidationResult
  ↓
analyticsPort.TrackOrder(@event)
```

#### Dependencies (Callee Calls)
- `IAnalyticsPort.TrackOrder()`
  - **Implementation:** `InMemoryAnalyticsAdapter`
  - **Effect:** Records order data in in-memory analytics store
  - **Note:** No idempotency key required (read-only tracking)

- `OrderPlacedHandlersHelper.GuardAndDeduplicateAsync()`
  - Validates event sequence order
  - Marks event as processed to prevent duplicates

#### Callers
- **DotNetCore.CAP Framework** (via `[CapSubscribe(EventTopics.OrderPlaced)]`)
  - CAP automatically discovers and registers this handler at application startup
  - Invoked when `OrderPlaced` event is published to RabbitMQ

#### Registration
```csharp
// Program.cs line 50
.AddSingleton<AnalyticsOnOrderPlacedHandler>()
```

---

## Helper Class: `OrderPlacedHandlersHelper`

#### Purpose
Centralizes the common guard and deduplication logic shared across all `OrderPlaced` handlers.

#### Method: `GuardAndDeduplicateAsync()`

**Signature:**
```csharp
public static async Task<ConsumerEventValidationResult?> GuardAndDeduplicateAsync(
    ILogger logger,
    IConsumerSequenceGuardStore sequenceGuardStore,
    IMessageDeduplicationStore deduplicationStore,
    string consumerName,
    IEventEnvelope @event,
    CancellationToken cancellationToken = default)
```

**Logic:**
1. Call `ConsumerEventGuard.ValidateAndLogAsync()`
   - Validates event has non-null `OrderId`
   - Checks event sequence number via `sequenceGuardStore`
   - Logs the processing event with decision
2. Check `validationResult.Decision == SequenceGuardDecision.Accept`
   - Return `null` if decision is `Duplicate` or `OutOfOrder`
   - Early exit prevents downstream business logic execution
3. Call `deduplicationStore.TryMarkProcessedAsync()`
   - Records that this consumer processed this event
   - Prevents duplicate side effects from message replays
   - Return `null` if event was already processed
4. Return the `ConsumerEventValidationResult` containing:
   - `OrderId`: The order ID from the event
   - `PartitionKey`: Derived from `@event.GetPartitionKey()` (returns `OrderId`)
   - `Decision`: The sequence guard decision

#### Callee Calls
- `ConsumerEventGuard.ValidateAndLogAsync()`
- `IConsumerSequenceGuardStore.CheckAndRecordAsync()`
- `IMessageDeduplicationStore.TryMarkProcessedAsync()`

---

## Event Flow: End-to-End

### Event Source (Caller → Handler)
```
CheckoutUseCase.PlaceOrderAsync()
  ↓
1. Create Order aggregate
2. Allocate sequence number
3. Create OrderPlaced event with metadata
4. Save order to database (EF Core)
5. eventBus.PublishAsync(orderPlaced, partitionKey)
  └─ Implementation: CapEventBus → RabbitMQ topic "OrderPlaced"
  └─ Partition Key: order.OrderId (ensures ordering per order)
  ↓
[RabbitMQ broadcasts to all subscribers of "OrderPlaced" topic]
  ↓
CAP Framework discovers and invokes:
├─ InventoryOnOrderPlacedHandler.HandleAsync()
├─ PaymentOnOrderPlacedHandler.HandleAsync()
└─ AnalyticsOnOrderPlacedHandler.HandleAsync()
  (may run in parallel, depending on CAP concurrency settings)
  ↓
Payment Handler publishes follow-up events:
├─ PaymentAuthorized → triggers ShippingOnPaymentAuthorizedHandler, NotifyOnPaymentAuthorizedHandler
└─ PaymentFailed → triggers CancelOrderOnPaymentFailedHandler
```

---

## Dependency Injection Graph

### Injected Into Handlers
```
IInventoryPort
  ↑ from Program.cs
  └─ InMemoryInventoryAdapter (Singleton)

IPaymentPort
  ↑ from Program.cs
  └─ InMemoryPaymentAdapter (Singleton)

IAnalyticsPort
  ↑ from Program.cs
  └─ InMemoryAnalyticsAdapter (Singleton)

IEventBus
  ↑ from Program.cs
  └─ CapEventBus (Singleton)
    └─ ICapPublisher (provided by CAP framework)

IConsumerSequenceGuardStore
  ↑ from Program.cs
  └─ EfCoreConsumerSequenceGuardStore (Scoped)
    └─ EcommerceDbContext

IMessageDeduplicationStore
  ↑ from Program.cs
  └─ EfCoreMessageDeduplicationStore (Scoped)
    └─ EcommerceDbContext

IOrderEventSequenceAllocator
  ↑ from Program.cs
  └─ EfCoreOrderEventSequenceAllocator (Scoped)
    └─ EcommerceDbContext

ILogger<THandler>
  ↑ from ASP.NET Core logging infrastructure
```

---

## Key Design Decisions

### 1. Separate Handlers for Each Concern
- **Inventory, Payment, and Analytics** are independent concerns
- Each handler can be scaled, tested, and modified independently
- CAP ensures they all receive the same event

### 2. Guard and Deduplication Pattern
- **Sequence Guard:** Ensures events are processed in order per order ID
- **Deduplication:** Prevents duplicate side effects if the same message is retried
- **Centralized via Helper:** Reduces boilerplate and ensures consistency

### 3. Transaction-Per-Publish
- **CheckoutUseCase:** Uses CAP transaction coordinator to wrap database save + event publish
- **Handlers:** Use their own DB context for recording deduplication/sequence state

### 4. Partition Key for Ordering
- **OrderId as Partition Key:** Ensures all events for an order are processed serially
- **Critical for:** Payment → Shipping → Notification ordering

### 5. Exception Handling in Payment Handler
- **try-catch:** Allows graceful failure handling
- **Publishes PaymentFailed:** Downstream handlers can react to failure
- **No exception thrown:** Prevents CAP from retrying (which would cause duplicate attempts)

---

## Architecture Test Constraints

**Constraint:** All `eventBus.PublishAsync()` calls must use one of:
- Literal variable named `partitionKey`
- `.OrderId` property access
- `.GetPartitionKey()` method call

**Reason:** Ensures OrderId partition affinity is maintained for message ordering

**Implementation:** See [EcommerceEventPublishingArchitectureTests.cs](../../tests/EventSourcingBankAccountWeb.Tests/EcommerceEventPublishingArchitectureTests.cs)

---

## Testing Considerations

### Unit Testing
- Mock `IInventoryPort`, `IPaymentPort`, `IAnalyticsPort`
- Mock `IEventBus`, `IConsumerSequenceGuardStore`, `IMessageDeduplicationStore`
- Test each handler method independently

### Integration Testing
- Use in-memory database (`DbContext`)
- Publish actual `OrderPlaced` events via CAP
- Verify side effects occurred (inventory reserved, payment authorized, etc.)

### Idempotency Testing
- Publish the same event twice
- Verify side effects execute only once
- Verify deduplication prevents duplicate records

---

## Related Files

- **Event Definitions:** [src/EcommerceCheckoutFlow/Domain/Events.cs](../src/EcommerceCheckoutFlow/Domain/Events.cs)
- **Ports:** [src/EcommerceCheckoutFlow/Application/Ports/](../src/EcommerceCheckoutFlow/Application/Ports/)
- **Event Bus Adapter:** [src/EcommerceCheckoutFlow/Adapters/Secondary/CapEventBus.cs](../src/EcommerceCheckoutFlow/Adapters/Secondary/CapEventBus.cs)
- **Consumer Event Guard:** [src/EcommerceCheckoutFlow/Application/Handlers/ConsumerEventGuard.cs](../src/EcommerceCheckoutFlow/Application/Handlers/ConsumerEventGuard.cs)
- **Use Case:** [src/EcommerceCheckoutFlow/Application/UseCases/CheckoutUseCase.cs](../src/EcommerceCheckoutFlow/Application/UseCases/CheckoutUseCase.cs)
- **Configuration:** [src/EcommerceCheckoutFlow/Program.cs](../src/EcommerceCheckoutFlow/Program.cs)
- **Tests:** [tests/EventSourcingBankAccountWeb.Tests/EcommerceEventPublishingArchitectureTests.cs](../tests/EventSourcingBankAccountWeb.Tests/EcommerceEventPublishingArchitectureTests.cs)
