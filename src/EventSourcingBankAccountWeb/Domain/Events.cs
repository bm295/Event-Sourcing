namespace EventSourcingBankAccountWeb.Domain;

public abstract record BankAccountEvent(
    string EventId,
    string StreamId,
    int SequenceNumber,
    DateTimeOffset CreatedAtUtc)
{
    public abstract string EventType { get; }
}

public sealed record AccountOpened(
    string EventId,
    string StreamId,
    int SequenceNumber,
    DateTimeOffset CreatedAtUtc,
    string OwnerName)
    : BankAccountEvent(EventId, StreamId, SequenceNumber, CreatedAtUtc)
{
    public override string EventType => nameof(AccountOpened);
}

public sealed record MoneyDeposited(
    string EventId,
    string StreamId,
    int SequenceNumber,
    DateTimeOffset CreatedAtUtc,
    decimal Amount)
    : BankAccountEvent(EventId, StreamId, SequenceNumber, CreatedAtUtc)
{
    public override string EventType => nameof(MoneyDeposited);
}

public sealed record MoneyWithdrawn(
    string EventId,
    string StreamId,
    int SequenceNumber,
    DateTimeOffset CreatedAtUtc,
    decimal Amount)
    : BankAccountEvent(EventId, StreamId, SequenceNumber, CreatedAtUtc)
{
    public override string EventType => nameof(MoneyWithdrawn);
}

public sealed record EventMetadata(
    string EventId,
    string StreamId,
    int SequenceNumber,
    DateTimeOffset CreatedAtUtc);
