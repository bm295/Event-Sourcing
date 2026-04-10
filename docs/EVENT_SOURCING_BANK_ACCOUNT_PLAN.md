# Event-Sourced Bank Account Console Demo

## Summary
Build a minimal console application that demonstrates event sourcing through a single `BankAccount` aggregate. The program should store only events, rebuild current state by replaying them, and show at least one read model derived from the same event stream. Keep the scope intentionally small: one account, one stream, no database, no framework-heavy infrastructure.

## Key Changes
- Use a single business domain: `BankAccount`.
- Commands: `OpenAccount`, `DepositMoney`, `WithdrawMoney`.
- Domain rules: account must be opened before use; deposit amount must be positive; withdrawal amount must be positive and cannot exceed current balance.
- Define a small event model:
  - `AccountOpened`
  - `MoneyDeposited`
  - `MoneyWithdrawn`
- Implement the write side as one aggregate that:
  - Handles commands against current state
  - Produces new domain events
  - Applies events to mutate in-memory state
  - Can be rehydrated entirely from prior events
- Implement the event store as the simplest viable version:
  - Append-only JSON-lines file or single JSON array file
  - One stream per account id
  - Load stream, replay events, append new events in order
- Implement one read model/projection:
  - `AccountBalanceView` rebuilt from events
  - Console output should display event history and current balance after each command
- Keep the UI trivial:
  - Menu-driven CLI with options to open account, deposit, withdraw, show balance, show event log, rebuild state from file
  - No authentication, no multiple users, no transfers, no interest, no overdraft feature

## Public Interfaces / Types
- `BankAccountCommand` types for the three commands above
- `BankAccountEvent` types for the three persisted events
- `BankAccount` aggregate with methods equivalent to `LoadFromHistory(events)` and command handlers that return new events
- `IEventStore` with minimal operations:
  - load events for a stream
  - append events to a stream
- `AccountBalanceProjection` or equivalent read-model builder

## Test Plan
- Opening a new account emits exactly `AccountOpened`
- Depositing after open emits `MoneyDeposited` and increases reconstructed balance
- Withdrawing within balance emits `MoneyWithdrawn` and decreases reconstructed balance
- Depositing zero or negative amount is rejected
- Withdrawing zero or negative amount is rejected
- Withdrawing more than current balance is rejected
- Depositing or withdrawing before account open is rejected
- Rehydrating from persisted events reproduces the same balance as the in-memory run
- Rebuilding the read model from the stored event log shows the expected final balance and ordered history

## Assumptions
- Target shape: console demo for learning, not production architecture
- Persistence: local file-based event store, not a database
- Concurrency/versioning: omitted to keep the first example minimal
- Projection updates: synchronous and rebuilt from the same event stream
- Single currency and integer or decimal money type; no exchange rates or fees
- One bank account stream is enough for v1; multi-account support is optional later once the event flow is understood
