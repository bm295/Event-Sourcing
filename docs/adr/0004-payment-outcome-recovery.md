# ADR 0004: Recover payment processing from a durable outcome

Status: Accepted for the payment consumer

## Problem

The payment subscriber previously advanced its sequence/deduplication records before authorization. A failure could therefore suppress the next delivery without finishing payment. Its catch block also treated a failed publish as a failed authorization.

## Decision

`PaymentOnOrderPlacedHandler` uses `IPaymentProcessingStore`, a payment-specific durable inbox and outcome journal. It does not use the old consumer sequence/deduplication pre-marking path.

1. Read the input event's journal entry. Return if completed.
2. If no outcome exists, authorize with the stable key `PaymentOnOrderPlacedHandler:<EventId>`.
3. Catch only `PaymentDeclinedException`, which means a definitive business rejection. Timeouts, cancellation and storage/transport errors propagate for retry.
4. Persist the typed outcome and its allocated per-order sequence in one SQLite transaction. The event ID, timestamp, correlation, causation, payload and sequence are immutable on redelivery.
5. Publish that saved event through `IEventBus`, using `OrderId` as partition key.
6. Mark the input completed only after publishing succeeds (CAP accepts the outgoing message into its storage; broker delivery is asynchronous).

The journal is scoped to this one payment consumer, keyed by input `EventId`. It is not a generic global sequence checkpoint. The subscriber observes only `OrderPlaced`, not every order event; it must not require a contiguous sequence of all order events.

The runtime demo uses `PersistentPaymentAdapter`: the simulated authorization and idempotency key are committed together in `payment_authorizations`. A real provider must persist the same idempotency contract, return the same definitive result for retries and reject conflicting requests with the same key. The local journal alone cannot make a remote charge exactly once.

## Recovery cases

| Failure point | Redelivery behavior |
| --- | --- |
| Before provider call | Retry authorization with the same key |
| Provider committed but response/journal write lost | Query/retry the provider with the same key, then save its result |
| Outcome saved, publish failed | Republish the saved event without calling the provider |
| Publish accepted, acknowledgment/checkpoint lost | Republish the identical event ID; downstream consumers must deduplicate |
| Completion committed, acknowledgment lost | Skip the input on redelivery |

Delivery remains at least once. Concurrent workers may attempt authorization or publication more than once; the provider's persistent key prevents a repeated authorization and the journal's primary key preserves one outcome. SQLite writer conflicts propagate for broker retry. This is not a distributed lock or an exactly-once guarantee.

The checkpoint and publish are intentionally separate: a saved outcome plus retried input recovers the gap. Exhausted CAP retries still require operator retry. Pending outcomes are not automatically scanned by a separate recovery worker.

## ENode reference

The implementation adapts these ENode ideas without adding an ENode dependency or copying its container/mailbox infrastructure:

- `DefaultEventCommittingService.TryToRepublishEventAsync`: find the already committed event and publish it again rather than execute the command again.
- `DefaultProcessingEventProcessor.DispatchProcessingMessageAsync`: dispatch first, advance the processed version after success.
- Event-store uniqueness for command identity/version, adapted here to a unique input-event journal entry and a transactionally allocated outcome sequence.

Reference revision: https://github.com/tangxuehua/enode/tree/d882070710a731b411c0ac559dd9d167459339e4

## Runtime and rollout

Subscribers implement `ICapSubscribe`, use explicit consumer groups for fan-out and have scoped lifetimes, as do the event bus and EF-backed ports. Sequence services and the cancellation subscriber are registered. Group names are now explicit: existing broker subscriptions must be reviewed during deployment; do not run old and new consumers concurrently without accounting for redelivery.

`PaymentProcessingSchema.EnsureCreatedAsync` adds the two tables idempotently on startup, including databases where EF `EnsureCreated` would otherwise skip schema creation. It preserves existing tables. It does not backfill historical authorizations from the old in-memory payment adapter or old `processed_messages`: those records cannot establish whether a real external payment occurred. Reconcile any historical pending payments before replaying them into this new consumer.

No payment-journal retention is configured. Retain journal entries and provider idempotency records for at least the supported redelivery/replay horizon. Deleting them can allow an old input to authorize again.

Inventory, analytics, shipping, cancellation and notification business processing still use the prior guard/deduplication design. Their crash-safety and broader per-order sequence design require a separate change; this ADR does not claim those consumers are repaired.

## Validation

`PaymentRecoveryGuardrailTests` uses a file-backed SQLite database and creates a fresh context/provider/journal on every delivery. It covers before/after authorization, before/after journal commit, publish failures before/after acknowledgment, completion failures, definitive decline, concurrent redelivery, conflicting provider keys and additive schema upgrade. Existing metadata and duplicate-delivery tests use the new payment-journal port.

CI uses .NET 10 to match the projects and runs the complete test suite.
