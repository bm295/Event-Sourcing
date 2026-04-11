using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Infrastructure;
using EventSourcingBankAccountWeb.Models;

namespace EventSourcingBankAccountWeb.Services;

public sealed class DemoStateService(IEventStore eventStore, IClock clock)
{
    private const string StreamId = "bank-account-demo";
    private readonly object _lock = new();
    private readonly AccountBalanceProjection _projection = new();

    public DemoState GetState()
    {
        lock (_lock)
        {
            return BuildState([]);
        }
    }

    public ExecuteCommandResponse ExecuteCommand(ExecuteCommandRequest request)
    {
        lock (_lock)
        {
            var flow = new List<FlowStep>();
            var account = new BankAccount();
            var history = eventStore.Load(StreamId);

            flow.Add(new FlowStep("Command Received", "command-panel", "application-service", $"UI emitted `{request.CommandType}`."));
            flow.Add(new FlowStep("Load Event Stream", "application-service", "event-store", $"Loaded {history.Count} historical event(s)."));

            account.LoadFromHistory(history);
            flow.Add(new FlowStep("Rehydrate Aggregate", "event-store", "bank-account", "Historical events replayed into the aggregate."));

            try
            {
                var nextSequence = history.Count + 1;
                var metadata = new EventMetadata(
                    Guid.NewGuid().ToString("N"),
                    StreamId,
                    nextSequence,
                    clock.UtcNow);

                var domainEvent = request.CommandType switch
                {
                    "OpenAccount" => account.OpenAccount(new OpenAccount(StreamId, request.OwnerName?.Trim() ?? "Demo User"), metadata),
                    "DepositMoney" => account.DepositMoney(new DepositMoney(StreamId, request.Amount ?? 0m), metadata),
                    "WithdrawMoney" => account.WithdrawMoney(new WithdrawMoney(StreamId, request.Amount ?? 0m), metadata),
                    _ => throw new DomainException("Unsupported command type.")
                };

                account.Apply(domainEvent);
                flow.Add(new FlowStep("Emit Domain Event", "bank-account", "application-service", $"Aggregate emitted `{domainEvent.EventType}`."));

                var record = eventStore.Append(domainEvent);
                flow.Add(new FlowStep("Persist Event", "application-service", "event-store", $"Event `{record.EventId}` stored with status `{record.Status}`."));

                eventStore.UpdateStatus(record.EventId, EventStatus.Projected);
                flow.Add(new FlowStep("Update Projection", "application-service", "account-balance-projection", $"Projection consumed `{record.EventType}`."));
                flow.Add(new FlowStep("Refresh Read Model", "account-balance-projection", "account-balance-view", "Read model rebuilt from stored events."));
                flow.Add(new FlowStep("Refresh Event Table", "event-store", "event-list-view", "Event list updated to reflect the latest status."));

                return new ExecuteCommandResponse(true, null, BuildState(flow), [record.EventId]);
            }
            catch (DomainException ex)
            {
                flow.Add(new FlowStep("Validation Error", "bank-account", "command-panel", ex.Message));
                return new ExecuteCommandResponse(false, ex.Message, BuildState(flow), []);
            }
        }
    }

    public DemoState Replay()
    {
        lock (_lock)
        {
            var events = eventStore.Load(StreamId);
            foreach (var record in eventStore.GetRecords(StreamId))
            {
                eventStore.UpdateStatus(record.EventId, EventStatus.Replayed);
            }

            var flow = new List<FlowStep>
            {
                new("Replay Requested", "command-panel", "application-service", "UI requested replay from stored events."),
                new("Load Event Stream", "application-service", "event-store", $"Loaded {events.Count} stored event(s)."),
                new("Rebuild Aggregate", "event-store", "bank-account", "Aggregate reconstructed from the ordered stream."),
                new("Rebuild Projection", "event-store", "account-balance-projection", "Projection rebuilt from the same ordered stream."),
                new("Refresh Views", "account-balance-projection", "account-balance-view", "Balance view and event table refreshed from replayed data.")
            };

            return BuildState(flow);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            eventStore.Reset(StreamId);
        }
    }

    private DemoState BuildState(IReadOnlyList<FlowStep> latestFlow)
    {
        var events = eventStore.Load(StreamId);
        var records = eventStore.GetRecords(StreamId);
        var projection = _projection.Build(events);

        var activeComponentIds = latestFlow
            .SelectMany(step => new[] { step.SourceComponentId, step.TargetComponentId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Cast<string>()
            .ToList();

        var activeConnectionIds = latestFlow
            .Where(step => !string.IsNullOrWhiteSpace(step.TargetComponentId))
            .Select(step => $"{step.SourceComponentId}->{step.TargetComponentId}")
            .Distinct()
            .ToList();

        return new DemoState(
            StreamId,
            new AccountState(
                projection.IsOpen,
                projection.Balance,
                projection.History
                    .Select(item => new TransactionHistoryItemDto(item.SequenceNumber, item.EventType, item.Description, item.CreatedAtUtc))
                    .ToList()),
            BuildComponents(),
            records,
            latestFlow,
            activeComponentIds,
            activeConnectionIds,
            clock.UtcNow);
    }

    private static IReadOnlyList<ComponentDescriptor> BuildComponents()
    {
        return
        [
            new("command-panel", "Command Panel", "User-facing controls that emit commands into the system.", ["OpenAccount", "DepositMoney", "WithdrawMoney", "Replay"], ["User input"]),
            new("application-service", "BankAccountApplicationService", "Coordinates loading history, dispatching commands, persisting events, and refreshing projections.", ["Persist requests", "Projection update calls", "Flow steps"], ["Commands", "Historical events", "New domain events"]),
            new("bank-account", "BankAccount", "Aggregate that enforces rules and emits domain events.", ["AccountOpened", "MoneyDeposited", "MoneyWithdrawn"], ["Commands", "Historical events"]),
            new("event-store", "EventStore", "Append-only store for the ordered event stream.", ["Historical event stream", "Event records"], ["New domain events"]),
            new("account-balance-projection", "AccountBalanceProjection", "Projection that converts domain events into read-model state.", ["AccountBalanceViewModel"], ["Domain events"]),
            new("account-balance-view", "AccountBalanceView", "Read model that shows balance and transaction history.", ["Visual balance updates"], ["Projection output"]),
            new("event-list-view", "EventListView", "Database-style grid of stored events and their statuses.", ["Event rows", "Selected event details"], ["Persisted event records"]),
            new("flow-inspector", "Flow Inspector", "Panel that explains what the selected component emits and consumes.", ["Component detail display"], ["Selected component metadata"])
        ];
    }
}
