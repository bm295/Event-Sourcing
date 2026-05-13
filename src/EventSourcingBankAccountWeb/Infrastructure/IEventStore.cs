using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Models;

namespace EventSourcingBankAccountWeb.Infrastructure;

public interface IEventStore
{
    IReadOnlyList<BankAccountEvent> Load(string streamId);
    EventRecord Append(BankAccountEvent @event);
    IReadOnlyList<EventRecord> GetRecords(string streamId);
}
