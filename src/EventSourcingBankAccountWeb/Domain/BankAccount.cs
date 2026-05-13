namespace EventSourcingBankAccountWeb.Domain;

public sealed class BankAccount
{
    public string AccountId { get; private set; } = string.Empty;
    public bool IsOpen { get; private set; }
    public decimal Balance { get; private set; }

    public void LoadFromHistory(IEnumerable<BankAccountEvent> events)
    {
        var expectedAggregateId = string.Empty;
        int? previousSequence = null;

        foreach (var @event in events)
        {
            if (string.IsNullOrWhiteSpace(expectedAggregateId))
            {
                expectedAggregateId = @event.AggregateId;
            }
            else if (!string.Equals(expectedAggregateId, @event.AggregateId, StringComparison.Ordinal))
            {
                throw new DomainException($"Replay stream contains mixed aggregates: expected '{expectedAggregateId}', but found '{@event.AggregateId}' at sequence {@event.SequenceNumber}.");
            }

            if (previousSequence.HasValue && @event.SequenceNumber <= previousSequence.Value)
            {
                throw new ReplaySequenceException(@event.AggregateId, previousSequence.Value, @event.SequenceNumber);
            }

            previousSequence = @event.SequenceNumber;
            Apply(@event);
        }
    }

    public BankAccountEvent OpenAccount(OpenAccount command, EventMetadata metadata)
    {
        if (IsOpen)
        {
            throw new DomainException("Account is already open.");
        }

        return new AccountOpened(
            metadata.EventId,
            metadata.StreamId,
            metadata.SequenceNumber,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.CreatedAtUtc,
            command.OwnerName);
    }

    public BankAccountEvent DepositMoney(DepositMoney command, EventMetadata metadata)
    {
        EnsureAccountIsOpen();

        if (command.Amount <= 0)
        {
            throw new DomainException("Deposit amount must be positive.");
        }

        return new MoneyDeposited(
            metadata.EventId,
            metadata.StreamId,
            metadata.SequenceNumber,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.CreatedAtUtc,
            command.Amount);
    }

    public BankAccountEvent WithdrawMoney(WithdrawMoney command, EventMetadata metadata)
    {
        EnsureAccountIsOpen();

        if (command.Amount <= 0)
        {
            throw new DomainException("Withdrawal amount must be positive.");
        }

        if (command.Amount > Balance)
        {
            throw new DomainException("Withdrawal amount cannot exceed current balance.");
        }

        return new MoneyWithdrawn(
            metadata.EventId,
            metadata.StreamId,
            metadata.SequenceNumber,
            metadata.CorrelationId,
            metadata.CausationId,
            metadata.CreatedAtUtc,
            command.Amount);
    }

    public void Apply(BankAccountEvent @event)
    {
        switch (@event)
        {
            case AccountOpened opened:
                AccountId = opened.AggregateId;
                IsOpen = true;
                break;
            case MoneyDeposited deposited:
                Balance += deposited.Amount;
                break;
            case MoneyWithdrawn withdrawn:
                Balance -= withdrawn.Amount;
                break;
        }
    }

    private void EnsureAccountIsOpen()
    {
        if (!IsOpen)
        {
            throw new DomainException("Account must be opened before use.");
        }
    }
}
