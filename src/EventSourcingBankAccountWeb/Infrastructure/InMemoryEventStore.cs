using System.Security.Cryptography;
using System.Text.Json;
using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Models;

namespace EventSourcingBankAccountWeb.Infrastructure;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<StoredEvent>> _streams = new();
    private readonly IEventDataSigner _signer;

    public InMemoryEventStore()
        : this(new HmacEventDataSigner(RandomNumberGenerator.GetBytes(32)))
    {
    }

    public InMemoryEventStore(IEventDataSigner signer)
    {
        _signer = signer;
    }

    public IReadOnlyList<BankAccountEvent> ReadStream(string streamId)
    {
        lock (_lock)
        {
            return GetStream(streamId)
                .OrderBy(e => e.SequenceNumber)
                .Select(VerifyAndRead)
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
                // Optimistic concurrency: reject writes from callers that loaded an older stream version.
                throw new EventStoreConcurrencyException(@event.StreamId, expectedSequence, currentSequence);
            }

            var expectedNextSequence = currentSequence + 1;
            if (@event.SequenceNumber <= currentSequence)
            {
                // Explicit stale-event guard: never allow replays/retries to append already-used or older sequence numbers.
                throw new EventStoreConcurrencyException(@event.StreamId, expectedNextSequence, @event.SequenceNumber);
            }

            if (@event.SequenceNumber != expectedNextSequence)
            {
                // Gap guard: sequence must be contiguous so replay is deterministic.
                throw new EventStoreConcurrencyException(@event.StreamId, expectedNextSequence, @event.SequenceNumber);
            }

            var payloadJson = JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions);
            var stored = new StoredEvent(@event, payloadJson, _signer.Sign(payloadJson), _signer.Algorithm);
            stream.Add(stored);
            return stored.ToRecord(signatureValid: true);
        }
    }

    public IReadOnlyList<EventRecord> ReadRecords(string streamId)
    {
        lock (_lock)
        {
            return GetStream(streamId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.ToRecord(_signer.Verify(e.PayloadJson, e.DataSignature)))
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

    private BankAccountEvent VerifyAndRead(StoredEvent storedEvent)
    {
        if (!_signer.Verify(storedEvent.PayloadJson, storedEvent.DataSignature))
        {
            throw new EventDataIntegrityException(storedEvent.EventId);
        }

        return storedEvent.DomainEvent;
    }

    private sealed class StoredEvent
    {
        public StoredEvent(BankAccountEvent domainEvent, string payloadJson, string dataSignature, string signatureAlgorithm)
        {
            DomainEvent = domainEvent;
            PayloadJson = payloadJson;
            DataSignature = dataSignature;
            SignatureAlgorithm = signatureAlgorithm;
        }

        public string EventId => DomainEvent.EventId;
        public int SequenceNumber => DomainEvent.SequenceNumber;
        public BankAccountEvent DomainEvent { get; }
        public string PayloadJson { get; }
        public string DataSignature { get; }
        public string SignatureAlgorithm { get; }

        public EventRecord ToRecord(bool signatureValid)
        {
            return new EventRecord(
                DomainEvent.EventId,
                DomainEvent.AggregateId,
                DomainEvent.StreamId,
                DomainEvent.SequenceNumber,
                DomainEvent.EventType,
                DomainEvent.CorrelationId,
                DomainEvent.CausationId,
                DomainEvent.CreatedAtUtc,
                PayloadJson,
                DataSignature,
                SignatureAlgorithm,
                signatureValid);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
