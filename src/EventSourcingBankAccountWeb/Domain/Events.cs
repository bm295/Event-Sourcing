namespace EventSourcingBankAccountWeb.Domain;

public abstract record BankAccountEvent(
    string EventId,
    string AggregateId,
    int SequenceNumber,
    string CorrelationId,
    string? CausationId,
    DateTimeOffset CreatedAtUtc)
{
    // Backward-compatibility mapping rule for existing stream-oriented APIs.
    public string StreamId => AggregateId;

    public abstract string EventType { get; }
}

public sealed record AccountOpened(
    string EventId,
    string AggregateId,
    int SequenceNumber,
    string CorrelationId,
    string? CausationId,
    DateTimeOffset CreatedAtUtc,
    string OwnerName)
    : BankAccountEvent(EventId, AggregateId, SequenceNumber, CorrelationId, CausationId, CreatedAtUtc)
{
    public override string EventType => nameof(AccountOpened);
}

public sealed record MoneyDeposited(
    string EventId,
    string AggregateId,
    int SequenceNumber,
    string CorrelationId,
    string? CausationId,
    DateTimeOffset CreatedAtUtc,
    decimal Amount)
    : BankAccountEvent(EventId, AggregateId, SequenceNumber, CorrelationId, CausationId, CreatedAtUtc)
{
    public override string EventType => nameof(MoneyDeposited);
}

public sealed record MoneyWithdrawn(
    string EventId,
    string AggregateId,
    int SequenceNumber,
    string CorrelationId,
    string? CausationId,
    DateTimeOffset CreatedAtUtc,
    decimal Amount)
    : BankAccountEvent(EventId, AggregateId, SequenceNumber, CorrelationId, CausationId, CreatedAtUtc)
{
    public override string EventType => nameof(MoneyWithdrawn);
}

public sealed record StateTransitionRejected(
    string EventId,
    string AggregateId,
    int SequenceNumber,
    string CorrelationId,
    string? CausationId,
    DateTimeOffset CreatedAtUtc,
    string CommandType,
    string Reason)
    : BankAccountEvent(EventId, AggregateId, SequenceNumber, CorrelationId, CausationId, CreatedAtUtc)
{
    public override string EventType => nameof(StateTransitionRejected);
}

public sealed record EventMetadata(
    string EventId,
    string AggregateId,
    int SequenceNumber,
    string CorrelationId,
    string? CausationId,
    DateTimeOffset CreatedAtUtc)
{
    // Backward-compatibility mapping rule for stream-oriented call sites.
    public string StreamId => AggregateId;
}
