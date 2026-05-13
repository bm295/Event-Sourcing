namespace EventSourcingBankAccountWeb.Domain;

public sealed class ReplaySequenceException(string aggregateId, int previousSequence, int currentSequence)
    : Exception($"Invalid event replay order for aggregate '{aggregateId}': sequence must be strictly increasing, but saw {currentSequence} after {previousSequence}.")
{
    public string AggregateId { get; } = aggregateId;
    public int PreviousSequence { get; } = previousSequence;
    public int CurrentSequence { get; } = currentSequence;
}
