using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Models;

namespace EventSourcingBankAccountWeb.Infrastructure;

/// <summary>
/// Phase 6 breaking change: event store contract is append/read-only.
/// </summary>
public interface IEventStore
{
    IReadOnlyList<BankAccountEvent> ReadStream(string streamId);
    EventRecord Append(BankAccountEvent @event, int expectedSequence);
    IReadOnlyList<EventRecord> ReadRecords(string streamId);
}
