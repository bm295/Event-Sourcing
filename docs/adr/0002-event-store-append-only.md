# ADR 0002: Event store append-only and read-only interface

## Status
Accepted — 2026-05-13

## Decision
- `IEventStore` is append/read-only (`Append`, `ReadStream`, `ReadRecords`).
- Runtime mutation APIs (`Update`, `Delete`, `Reset`, `Truncate`, `Purge`) are forbidden.
- Every state transition is represented as a new domain event.
- Rejected transitions are persisted as `StateTransitionRejected` events.
- Reset behavior is allowed only in test fixtures, not in production runtime paths.

## Consequences
- Safer replay and auditability.
- Breaking change for integrations using old `Load` and `GetRecords` names.
- Migration from UI-only status history to event-log based transition history.
