using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Infrastructure;
using EventSourcingBankAccountWeb.Models;
using EventSourcingBankAccountWeb.Services;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class Phase5GuardrailsTests
{
    [Fact]
    public void cannot_update_existing_event()
    {
        var methodNames = typeof(IEventStore).GetMethods().Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Update", methodNames);
        Assert.DoesNotContain("UpdateEvent", methodNames);
        Assert.DoesNotContain("Replace", methodNames);
    }

    [Fact]
    public void cannot_delete_stream_events()
    {
        var methodNames = typeof(IEventStore).GetMethods().Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Delete", methodNames);
        Assert.DoesNotContain("DeleteStream", methodNames);
        Assert.DoesNotContain("Reset", methodNames);
        Assert.DoesNotContain("Truncate", methodNames);
        Assert.DoesNotContain("Purge", methodNames);
    }

    [Fact]
    public void append_only_sequence_increases()
    {
        var store = new InMemoryEventStore();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00");

        var first = new AccountOpened("e1", "acc-1", 1, now, "owner");
        var second = new MoneyDeposited("e2", "acc-1", 2, now.AddMinutes(1), 100m);

        var r1 = store.Append(first, expectedSequence: 0);
        var r2 = store.Append(second, expectedSequence: 1);

        Assert.Equal(1, r1.SequenceNumber);
        Assert.Equal(2, r2.SequenceNumber);

        var invalid = new MoneyDeposited("e3", "acc-1", 2, now.AddMinutes(2), 20m);
        Assert.Throws<EventStoreConcurrencyException>(() => store.Append(invalid, expectedSequence: 2));
    }

    [Fact]
    public void replay_produces_same_state()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        var store = new InMemoryEventStore();
        var service = new DemoStateService(store, clock);

        service.ExecuteCommand(new ExecuteCommandRequest("OpenAccount", null, "Alice"));
        service.ExecuteCommand(new ExecuteCommandRequest("DepositMoney", 100m, null));
        service.ExecuteCommand(new ExecuteCommandRequest("WithdrawMoney", 40m, null));

        var beforeReplay = service.GetState();
        var replayed = service.Replay();

        Assert.Equal(beforeReplay.Account.IsOpen, replayed.Account.IsOpen);
        Assert.Equal(beforeReplay.Account.Balance, replayed.Account.Balance);
        Assert.Equal(beforeReplay.Events.Count, replayed.Events.Count);

        var beforeSeq = beforeReplay.Account.History.Select(x => x.SequenceNumber).ToArray();
        var replaySeq = replayed.Account.History.Select(x => x.SequenceNumber).ToArray();
        Assert.Equal(beforeSeq, replaySeq);
    }

    [Fact]
    public void workflow_state_change_always_creates_new_event()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        var store = new InMemoryEventStore();
        var service = new DemoStateService(store, clock);

        var initialCount = service.GetState().Events.Count;

        var open = service.ExecuteCommand(new ExecuteCommandRequest("OpenAccount", null, "Alice"));
        var afterOpenCount = service.GetState().Events.Count;

        var deposit = service.ExecuteCommand(new ExecuteCommandRequest("DepositMoney", 50m, null));
        var afterDepositCount = service.GetState().Events.Count;

        Assert.True(open.Success);
        Assert.True(deposit.Success);
        Assert.Equal(initialCount + 1, afterOpenCount);
        Assert.Equal(afterOpenCount + 1, afterDepositCount);
        Assert.NotEqual(service.GetState().Events[^2].EventId, service.GetState().Events[^1].EventId);
    }

    [Fact]
    public void concurrent_append_handles_optimistic_concurrency()
    {
        var store = new InMemoryEventStore();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00");

        store.Append(new AccountOpened("e1", "acc-1", 1, now, "owner"), expectedSequence: 0);

        var t1 = Task.Run(() =>
        {
            try
            {
                store.Append(new MoneyDeposited("e2", "acc-1", 2, now.AddMinutes(1), 10m), expectedSequence: 1);
                return true;
            }
            catch (EventStoreConcurrencyException)
            {
                return false;
            }
        });

        var t2 = Task.Run(() =>
        {
            try
            {
                store.Append(new MoneyDeposited("e3", "acc-1", 2, now.AddMinutes(2), 20m), expectedSequence: 1);
                return true;
            }
            catch (EventStoreConcurrencyException)
            {
                return false;
            }
        });

        Task.WaitAll(t1, t2);

        var successCount = new[] { t1.Result, t2.Result }.Count(x => x);
        Assert.Equal(1, successCount);
        Assert.Equal(2, store.GetRecords("acc-1").Count);
    }

    [Fact]
    public void architecture_forbids_mutable_event_store_dependencies()
    {
        var forbidden = new[] { "Update", "Delete", "Reset", "Truncate", "Purge" };
        var methods = typeof(IEventStore).GetMethods().Select(m => m.Name).ToArray();

        foreach (var name in methods)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void architecture_forbids_command_handler_editing_past_events()
    {
        var body = File.ReadAllText(Path.Combine("..", "..", "..", "..", "src", "EventSourcingBankAccountWeb", "Services", "DemoStateService.cs"));

        Assert.DoesNotContain("Update(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Delete(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Reset(", body, StringComparison.Ordinal);
        Assert.Contains("Append(", body, StringComparison.Ordinal);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;
    }
}
