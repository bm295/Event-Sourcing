namespace EventSourcingBankAccountWeb.Infrastructure;

public sealed class EventStoreConcurrencyException(string streamId, int expectedSequence, int actualSequence)
    : Exception($"Optimistic concurrency conflict for stream '{streamId}': expected current sequence {expectedSequence}, but found {actualSequence}.")
{
    public string StreamId { get; } = streamId;
    public int ExpectedSequence { get; } = expectedSequence;
    public int ActualSequence { get; } = actualSequence;
}
