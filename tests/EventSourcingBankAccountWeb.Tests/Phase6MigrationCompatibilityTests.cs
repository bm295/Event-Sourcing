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
    }

    private sealed class FixedClock(DateTimeOffset now) : EventSourcingBankAccountWeb.Infrastructure.IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
