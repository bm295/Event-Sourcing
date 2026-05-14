# Event-Sourced Bank Account Web Demo

## Summary
Build a simple web application that demonstrates event sourcing through a single `BankAccount` aggregate. The application visualizes how commands become immutable events, how events move through the system, and which components emit or consume those events. The user can click on a component in the UI and inspect its role in the event flow. The application also shows a database-style list of all persisted event envelopes and their metadata. Scope remains intentionally small: one account, one stream, in-memory persistence, and lightweight ASP.NET Core infrastructure.

## Goal
The primary goal is not banking features. The primary goal is to make event flow visible and understandable:
- which component receives a command
- which component emits a domain event
- which component persists the event
- which component consumes the event to rebuild state or projections
- how the same event stream drives both write-side state and read-side views
- how a persisted event list can be inspected the way a user would inspect rows in a database table
- how sequence numbers, correlation IDs, and causation IDs support deterministic replay and workflow tracing

## User Experience
- Show a simple page with a visual layout of the main components:
  - `Command Panel`
  - `BankAccountApplicationService`
  - `BankAccount`
  - `EventStore`
  - `AccountBalanceProjection`
  - `AccountBalanceView`
- Let the user trigger commands from the UI:
  - `OpenAccount`
  - `DepositMoney`
  - `WithdrawMoney`
- Show a live event timeline or event log as commands are executed.
- Show an event table that lists every created event with database-like metadata.
- Draw visible connections between components so event movement is easy to follow.
- Let the user click any component box/node to open a detail panel that explains:
  - what the component does
  - what it emits
  - what it consumes
  - which step of the flow it participates in
- Highlight the active path when a command is executed so the user can watch the event flow through the system.
- Support replay/rebuild so the user can see the system reconstruct state from stored events.
- Let the user inspect an individual event row to see its payload and status progression.

## Event Flow
1. The user clicks a command button in the `Command Panel`.
2. `Command Panel` emits a command to `BankAccountApplicationService`.
3. `BankAccountApplicationService` loads prior events from `EventStore`.
4. `BankAccount` consumes those historical events to rebuild current state.
5. `BankAccountApplicationService` sends the new command to `BankAccount`.
6. `BankAccount` validates business rules. Valid commands emit one successful domain event:
   - `OpenAccount` -> `AccountOpened`
   - `DepositMoney` -> `MoneyDeposited`
   - `WithdrawMoney` -> `MoneyWithdrawn`
7. Invalid commands raise a domain rejection; `BankAccountApplicationService` persists `StateTransitionRejected` as a new event instead of mutating prior events.
8. `BankAccountApplicationService` appends the emitted event or rejection event to `EventStore` with the expected stream sequence.
9. `BankAccountApplicationService` forwards the append-only stream to `AccountBalanceProjection`.
10. `AccountBalanceProjection` consumes the stream and emits updated read-model state.
11. `AccountBalanceView` consumes the read-model state and updates the displayed balance/history.
12. `EventListView` displays the newly persisted event as a new row with current status.
13. The UI highlights the components and connections involved in that flow.

## Replay / Rebuild Flow
1. The user clicks `Replay Events` or `Rebuild State`.
2. `EventStore` emits the full ordered stream for the account.
3. `BankAccount` consumes the stream to reconstruct write-side state.
4. `AccountBalanceProjection` consumes the same stream to reconstruct read-side state.
5. `AccountBalanceView` refreshes from the rebuilt projection.
6. `EventListView` refreshes the full ordered event table from the stored stream.
7. The UI shows that current state comes entirely from replayed events.

## Component Responsibilities
- `Command Panel`
  - Emits commands from user actions
  - Shows available commands and input fields for amounts
- `BankAccountApplicationService`
  - Consumes commands from the UI
  - Loads historical events from `EventStore`
  - Rehydrates `BankAccount`
  - Consumes newly emitted domain events from `BankAccount`
  - Creates `StateTransitionRejected` when a command violates domain rules
  - Persists successful and rejected transitions to `EventStore`
  - Forwards events to `AccountBalanceProjection`
  - Emits UI-facing flow steps for visualization
- `BankAccount`
  - Consumes commands from `BankAccountApplicationService`
  - Consumes historical events during rehydration
  - Emits `AccountOpened`, `MoneyDeposited`, `MoneyWithdrawn`
  - Rejects invalid transitions through domain exceptions
  - Applies those same events to update in-memory aggregate state
- `EventStore`
  - Consumes new domain events for append
  - Enforces append-only behavior, expected sequence, and contiguous stream ordering
  - Emits ordered historical events during load or replay
- `AccountBalanceProjection`
  - Consumes domain events
  - Emits `AccountBalanceViewModel` or equivalent read model
- `AccountBalanceView`
  - Consumes projection output
  - Renders balance, account status, and event history
- `EventListView`
  - Consumes persisted event records from `EventStore` or UI state
  - Renders all created events in a table/grid
  - Shows event metadata and status
- `Flow Inspector` or component detail panel
  - Consumes selected component state from the UI
  - Renders what that component emits, consumes, and why it exists

## Event Producer / Consumer Matrix
| Component | Emits | Consumes |
| --- | --- | --- |
| `Command Panel` | Commands | User input |
| `BankAccountApplicationService` | Persist calls, projection update calls, flow-step notifications, `StateTransitionRejected` | Commands, historical events, new domain events, domain rejections |
| `BankAccount` | `AccountOpened`, `MoneyDeposited`, `MoneyWithdrawn`, domain rejection signal | Commands, historical events |
| `EventStore` | Historical event stream | New domain events |
| `AccountBalanceProjection` | `AccountBalanceViewModel` | Domain events |
| `AccountBalanceView` | Visual state updates | Projection output |
| `EventListView` | Event table rows, selected event details | Persisted event records |
| `Flow Inspector` | Component detail display | Selected component metadata |

## Event List / Database View
- Show all created events in a table that feels like inspecting persisted rows in a database.
- Each row should include at least:
  - `EventId`
  - `AggregateId`
  - `StreamId`
  - `SequenceNumber`
  - `EventType`
  - `CorrelationId`
  - `CausationId`
  - `Status`
  - `CreatedAt`
  - `Payload`
- `Status` should be simple but visible. Suggested statuses:
  - `New`
  - `Persisted`
  - `Projected`
  - `Replayed`
- The table should update after each command.
- The user should be able to click a row to inspect the full event payload and status history.
- If the implementation keeps only one current status, the UI should still make clear which pipeline stage the event has reached.

## UI Requirements
- Render components as clearly separated boxes, cards, or nodes.
- Render arrows or connectors between components.
- Support click interaction on each component.
- Show a side panel, modal, or inspector area for the selected component.
- Show the selected component's:
  - name
  - responsibility
  - emitted events/messages
  - consumed events/messages
  - current role in the last executed flow
- Animate or highlight the path used by the most recent command.
- Show the ordered event stream in a dedicated panel.
- Show an event table/grid with database-style rows and statuses.
- Support selecting an event row to inspect its payload and metadata.
- Show the reconstructed account balance and status.
- Keep the styling simple but intentionally visual enough that flow is obvious at a glance.

## Key Changes
- Change the deliverable from a console demo to a small web application.
- Keep the same business domain: `BankAccount`.
- Keep the same commands:
  - `OpenAccount`
  - `DepositMoney`
  - `WithdrawMoney`
- Keep the same domain rules:
  - account must be opened before use
  - deposit amount must be positive
  - withdrawal amount must be positive and cannot exceed current balance
- Persist both successful and rejected transitions as immutable events.
- Keep the successful domain events:
  - `AccountOpened`
  - `MoneyDeposited`
  - `MoneyWithdrawn`
- Add the rejected-transition domain event:
  - `StateTransitionRejected`
- Add UI state to visualize:
  - active command
  - active event flow
  - selected component details
  - event timeline
  - event table rows and selected event details
  - current read model
- Keep persistence simple:
  - in-memory for the first cut, or
  - local JSON file if the app already has a lightweight backend/server layer

## Suggested Architecture
- Frontend
  - Single-page UI with a component diagram, event table, and detail panel
  - Small state container for selected component, selected event, event log, active flow, event statuses, and read model
- Domain
  - `BankAccount` aggregate
  - command types
  - event types
- Application
  - `BankAccountApplicationService`
  - flow-step publisher for UI visualization
- Infrastructure
  - `IEventStore`
  - append/read-only in-memory event store with optimistic concurrency and contiguous sequence checks
- Projection
  - `AccountBalanceProjection`
  - optional transaction history projection for display

## Event Record Shape
- `EventRecord`
  - `eventId`
  - `aggregateId`
  - `streamId`
  - `sequenceNumber`
  - `eventType`
  - `correlationId`
  - `causationId`
  - `payload`
  - `createdAt`
  - `status`
  - `statusHistory` optional for richer visualization

## Public Interfaces / Types
- `BankAccountCommand`
- `BankAccountEvent`
- `StateTransitionRejected`
- `BankAccount`
- `BankAccountApplicationService`
- `IEventStore`
- `AccountBalanceProjection`
- `AccountBalanceViewModel`
- `EventRecord`
- `EventStatus`
- `FlowStep`
- `ComponentDescriptor`
  - component id
  - display name
  - description
  - emits
  - consumes
- `EventStoreConcurrencyException`
- `ReplaySequenceException`

## Suggested Runtime Sequence
1. User clicks a command button.
2. UI dispatches command to `BankAccountApplicationService`.
3. Service records flow step: command received.
4. Service loads event stream from `EventStore`.
5. Aggregate rehydrates from prior events.
6. Service dispatches command to aggregate.
7. Aggregate emits a new domain event, or the application service converts a domain rejection into `StateTransitionRejected`.
8. Service records flow step: event emitted by aggregate.
9. Service appends event to `EventStore`.
10. UI updates the event row status to `Persisted`.
11. Service records flow step: event persisted.
12. Service forwards event to `AccountBalanceProjection`.
13. Projection updates read model.
14. UI updates the event row status to `Projected`.
15. UI refreshes balance, event log, event table, and highlighted component path.

## Minimal Feature Set
- Execute `OpenAccount`
- Execute `DepositMoney`
- Execute `WithdrawMoney`
- Show validation errors in the UI
- Show full ordered event list
- Show all events in a table with status
- Show current balance
- Click a component to inspect what it emits and consumes
- Click an event row to inspect its payload and metadata
- Replay all events and rebuild state
- Highlight the latest event path

## Test Plan
- Clicking `OpenAccount` produces exactly `AccountOpened`
- Clicking `DepositMoney` after open produces `MoneyDeposited`
- Clicking `WithdrawMoney` within balance produces `MoneyWithdrawn`
- Depositing zero or negative amount shows a validation error
- Withdrawing zero or negative amount shows a validation error
- Withdrawing more than current balance shows a validation error
- Depositing or withdrawing before opening the account shows a validation error
- Invalid transitions append `StateTransitionRejected` with correlation and causation metadata
- Replaying the stored event stream reconstructs the same balance as the live session
- Every created event appears in the event table with the expected metadata
- Event stream append rejects stale expected sequence values and non-contiguous sequence numbers
- Persisted events show the correct current status in the UI
- Clicking an event row shows the correct payload and identifiers
- Clicking each component shows the correct emits/consumes details
- The highlighted flow for a command includes the expected producer and consumer components
- The event timeline remains in persisted order

## Assumptions
- Target shape: simple educational web app, not production architecture
- Single account stream is enough for v1
- Concurrency/versioning is intentionally minimal but present through expected stream sequence checks
- Projection updates are synchronous
- A minimal frontend stack is acceptable
- Fancy graph layout is unnecessary; a clear static diagram is enough
- The main success criterion is that a user can see and understand how events move between components
- Event status can be simulated in UI state even if the first cut uses only in-memory persistence
