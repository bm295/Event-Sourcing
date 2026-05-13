# EventSourcingBankAccountWeb

This project is a small ASP.NET Core web demo that visualizes event sourcing with a single `BankAccount` aggregate.

## Event-Sourcing Principles (Phase 6)
- **Event store is append-only**: existing events are never updated or deleted.
- **State change = new event**: every business transition, including rejected transitions, is persisted as a new event.
- **Reset is test-only fixture behavior**: there is no reset endpoint on the production runtime path.

## What It Shows
- command flow through application components
- which components emit events
- which components consume events
- a database-style event table with event status
- replay/rebuild from the stored event stream

## Prerequisites
- .NET SDK 10.0 preview or newer

## Run
From the repository root:

```powershell
dotnet run --project .\src\EventSourcingBankAccountWeb\EventSourcingBankAccountWeb.csproj
```

## Build
```powershell
dotnet build .\MultiThreadAutoResetEvent.sln
```

## How To Use
1. Click `Open Account`.
2. Click `Deposit Money` or `Withdraw Money`.
3. Click a component card to inspect what it emits and consumes.
4. Click an event row to inspect its payload and status history.
5. Click `Replay Events` to rebuild the state from stored events.

## Notes
- Persistence is in-memory for this version.
- Event statuses are visualized in the UI as `New`, `Persisted`, `Projected`, and `Replayed`.
- The project is isolated under `src/EventSourcingBankAccountWeb` so more advanced event-sourcing demos can be added later as separate projects.
