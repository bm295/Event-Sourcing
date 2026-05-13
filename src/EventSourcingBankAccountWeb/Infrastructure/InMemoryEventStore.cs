using System.Text.Json;
using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Models;

namespace EventSourcingBankAccountWeb.Infrastructure;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<StoredEvent>> _streams = new();

    public IReadOnlyList<BankAccountEvent> ReadStream(string streamId)
    {
        lock (_lock)
        {
            return GetStream(streamId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.DomainEvent)
                .ToList();
        }
    }

    public EventRecord Append(BankAccountEvent @event, int expectedSequence)
    {
        lock (_lock)
        {
            var stream = GetStream(@event.StreamId);
            var currentSequence = stream.Count == 0 ? 0 : stream[^1].SequenceNumber;
            if (currentSequence != expectedSequence)
            {
                throw new EventStoreConcurrencyException(@event.StreamId, expectedSequence, currentSequence);
            }

            var expectedNextSequence = currentSequence + 1;
            if (@event.SequenceNumber != expectedNextSequence)
            {
                throw new EventStoreConcurrencyException(@event.StreamId, expectedNextSequence, @event.SequenceNumber);
            }

            var stored = new StoredEvent(@event);
            stream.Add(stored);
            return stored.ToRecord();
        }
    }

    public IReadOnlyList<EventRecord> ReadRecords(string streamId)
    {
        lock (_lock)
        {
            return GetStream(streamId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.ToRecord())
                .ToList();
        }
    }



    private List<StoredEvent> GetStream(string streamId)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
        {
            stream = [];
            _streams[streamId] = stream;
        }

        return stream;
    }

    private sealed class StoredEvent
    {
        public StoredEvent(BankAccountEvent domainEvent)
        {
            DomainEvent = domainEvent;
        }

        public string EventId => DomainEvent.EventId;
        public int SequenceNumber => DomainEvent.SequenceNumber;
        public BankAccountEvent DomainEvent { get; }

        public EventRecord ToRecord()
        {
            return new EventRecord(
                DomainEvent.EventId,
                DomainEvent.StreamId,
                DomainEvent.SequenceNumber,
                DomainEvent.EventType,
                DomainEvent.CreatedAtUtc,
                JsonSerializer.Serialize(DomainEvent, DomainEvent.GetType(), JsonOptions));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
