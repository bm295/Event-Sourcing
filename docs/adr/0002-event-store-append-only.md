# ADR 0002: Event store append-only and read-only interface

## Status
Accepted — 2026-05-13

## Decision
- `IEventStore` is append/read-only (`Append`, `ReadStream`, `ReadRecords`).
- Runtime mutation APIs (`Update`, `Delete`, `Reset`, `Truncate`, `Purge`) are forbidden.
- Every state transition is represented as a new domain event.
- Rejected transitions are persisted as `StateTransitionRejected` events.
- Reset behavior is allowed only in test fixtures, not in production runtime paths.

- Persisted events must use a mandatory immutable envelope contract with `EventId`, `AggregateId`/`StreamId`, `EventType`, `SequenceNumber`, `CreatedAtUtc`, `CorrelationId`, `CausationId`, and payload.
- `CorrelationId` is created at command entry and propagated unchanged to all events in the same workflow.
- `CausationId` must always reference the immediate upstream cause (command id or event id) for each emitted event.

## Consequences
- Safer replay and auditability.
- Breaking change for integrations using old `Load` and `GetRecords` names.
- Migration from UI-only status history to event-log based transition history.

## Ordering vs lineage interpretation
- `SequenceNumber` defines append/replay order within a single stream and is the source of deterministic reconstruction.
- `CorrelationId` and `CausationId` define cross-step lineage and explain how a workflow traversed commands and events.
- Both are required: ordering without lineage weakens diagnostics; lineage without ordering breaks replay determinism.
