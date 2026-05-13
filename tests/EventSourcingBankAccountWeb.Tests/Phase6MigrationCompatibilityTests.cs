using EventSourcingBankAccountWeb.Infrastructure;
using EventSourcingBankAccountWeb.Models;
using EventSourcingBankAccountWeb.Services;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class Phase6MigrationCompatibilityTests
{
    [Fact]
    public void event_store_interface_is_append_and_read_only_breaking_change()
    {
        var methods = typeof(IEventStore).GetMethods().Select(m => m.Name).ToArray();
        Assert.Contains("ReadStream", methods);
        Assert.Contains("ReadRecords", methods);
        Assert.DoesNotContain("Load", methods);
        Assert.DoesNotContain("GetRecords", methods);
    }

    [Fact]
    public void failed_transition_is_migrated_to_append_only_event_log()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        var store = new InMemoryEventStore();
        var service = new DemoStateService(store, clock);

        var result = service.ExecuteCommand(new ExecuteCommandRequest("WithdrawMoney", 10m, null));

        Assert.False(result.Success);
        var state = service.GetState();
        Assert.Single(state.Events);
        Assert.Equal("StateTransitionRejected", state.Events[0].EventType);
        Assert.False(string.IsNullOrWhiteSpace(state.Events[0].CorrelationId));
        Assert.False(string.IsNullOrWhiteSpace(state.Events[0].CausationId));
    }

    [Fact]
    public void end_to_end_commands_preserve_correlation_and_causation_chain_across_records()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"));
        var store = new InMemoryEventStore();
        var service = new DemoStateService(store, clock);

        service.ExecuteCommand(new ExecuteCommandRequest("OpenAccount", null, "Alice", "cmd-1"));
        service.ExecuteCommand(new ExecuteCommandRequest("DepositMoney", 100m, null, "cmd-2"));
        service.ExecuteCommand(new ExecuteCommandRequest("WithdrawMoney", 30m, null, "cmd-3"));
        service.ExecuteCommand(new ExecuteCommandRequest("WithdrawMoney", 1000m, null, "cmd-4"));

        var records = store.ReadRecords("bank-account-demo");
        Assert.Equal(4, records.Count);

        var sequenceByAggregate = records.GroupBy(r => r.AggregateId);
        foreach (var group in sequenceByAggregate)
        {
            var ordered = group.OrderBy(r => r.SequenceNumber).ToArray();
            for (var i = 1; i < ordered.Length; i++)
            {
                Assert.True(ordered[i].SequenceNumber > ordered[i - 1].SequenceNumber);
            }
        }

        var byCausation = records.GroupBy(r => r.CausationId).ToDictionary(g => g.Key!, g => g.ToList());
        Assert.Equal(4, byCausation.Count);
        Assert.All(byCausation, pair =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Key));
            Assert.Single(pair.Value.Select(r => r.CorrelationId).Distinct());
        });

        Assert.Equal("cmd-1", records[0].CausationId);
        Assert.Equal("cmd-2", records[1].CausationId);
        Assert.Equal("cmd-3", records[2].CausationId);
        Assert.Equal("cmd-4", records[3].CausationId);
        Assert.Equal("StateTransitionRejected", records[3].EventType);

        Assert.Throws<EventStoreConcurrencyException>(() =>
            store.Append(new EventSourcingBankAccountWeb.Domain.MoneyDeposited(
                "manual-stale",
                "bank-account-demo",
                3,
                DateTimeOffset.Parse("2026-01-01T00:10:00+00:00"),
                5m),
            expectedSequence: 4));
    }

    private sealed class FixedClock(DateTimeOffset now) : EventSourcingBankAccountWeb.Infrastructure.IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
