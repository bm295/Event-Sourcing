using System.Text.Json;
using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Models;

namespace EventSourcingBankAccountWeb.Infrastructure;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<StoredEvent>> _streams = new();

    public IReadOnlyList<BankAccountEvent> Load(string streamId)
    {
        lock (_lock)
        {
            return GetStream(streamId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.DomainEvent)
                .ToList();
        }
    }

    public EventRecord Append(BankAccountEvent @event)
    {
        lock (_lock)
        {
            var stream = GetStream(@event.StreamId);
            var stored = new StoredEvent(@event, [EventStatus.New, EventStatus.Persisted]);
            stream.Add(stored);
            return stored.ToRecord();
        }
    }

    public IReadOnlyList<EventRecord> GetRecords(string streamId)
    {
        lock (_lock)
        {
            return GetStream(streamId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.ToRecord())
                .ToList();
        }
    }

    public void UpdateStatus(string eventId, EventStatus status)
    {
        lock (_lock)
        {
            foreach (var stream in _streams.Values)
            {
                var existing = stream.FirstOrDefault(e => e.EventId == eventId);
                if (existing is null)
                {
                    continue;
                }

                if (!existing.StatusHistory.Contains(status))
                {
                    existing.StatusHistory.Add(status);
                }

                return;
            }
        }
    }

    public void Reset(string streamId)
    {
        lock (_lock)
        {
            _streams.Remove(streamId);
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
        public StoredEvent(BankAccountEvent domainEvent, List<EventStatus> statusHistory)
        {
            DomainEvent = domainEvent;
            StatusHistory = statusHistory;
        }

        public string EventId => DomainEvent.EventId;
        public int SequenceNumber => DomainEvent.SequenceNumber;
        public BankAccountEvent DomainEvent { get; }
        public List<EventStatus> StatusHistory { get; }

        public EventRecord ToRecord()
        {
            return new EventRecord(
                DomainEvent.EventId,
                DomainEvent.StreamId,
                DomainEvent.SequenceNumber,
                DomainEvent.EventType,
                DomainEvent.CreatedAtUtc,
                StatusHistory.Last(),
                StatusHistory.ToList(),
                JsonSerializer.Serialize(DomainEvent, DomainEvent.GetType(), JsonOptions));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
