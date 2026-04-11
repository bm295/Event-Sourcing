# EventSourcingBankAccountWeb

This project is a small ASP.NET Core web demo that visualizes event sourcing with a single `BankAccount` aggregate.

## What It Shows
- command flow through application components
- which components emit events
- which components consume events
- a database-style event table with event status
- replay/rebuild from the stored event stream

## Project Files
- [event-sourcing-bank-account-plan.md](/d:/Code/MultiThreadAutoResetEvent/src/EventSourcingBankAccountWeb/event-sourcing-bank-account-plan.md)
- [Program.cs](/d:/Code/MultiThreadAutoResetEvent/src/EventSourcingBankAccountWeb/Program.cs)
- [wwwroot/index.html](/d:/Code/MultiThreadAutoResetEvent/src/EventSourcingBankAccountWeb/wwwroot/index.html)

## Prerequisites
- .NET SDK 10.0 preview or newer

## Run
From the repository root:

```powershell
dotnet run --project .\src\EventSourcingBankAccountWeb\EventSourcingBankAccountWeb.csproj
```

Or from the project folder:

```powershell
cd .\src\EventSourcingBankAccountWeb
dotnet run
```

After startup, open the local URL printed by ASP.NET Core, usually:

```text
http://localhost:5000
```

If a different port is shown in the console, use that one.

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
6. Click `Reset` to clear the in-memory event stream.

## Notes
- Persistence is in-memory for this version.
- Event statuses are visualized in the UI as `New`, `Persisted`, `Projected`, and `Replayed`.
- The project is isolated under `src/EventSourcingBankAccountWeb` so more advanced event-sourcing demos can be added later as separate projects.
