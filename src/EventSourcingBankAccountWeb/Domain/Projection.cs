namespace EventSourcingBankAccountWeb.Domain;

public sealed class AccountBalanceProjection
{
    public AccountBalanceViewModel Build(IEnumerable<BankAccountEvent> events)
    {
        var account = new BankAccount();
        var replayEvents = events.ToList();
        account.LoadFromHistory(replayEvents);

        var history = replayEvents
            .Select(e => new TransactionHistoryItem(e.SequenceNumber, e.EventType, Describe(e), e.CreatedAtUtc))
            .ToList();

        return new AccountBalanceViewModel(account.IsOpen, account.Balance, history);
    }

    private static string Describe(BankAccountEvent @event)
    {
        return @event switch
        {
            AccountOpened opened => $"Owner: {opened.OwnerName}",
            MoneyDeposited deposited => $"Amount: {deposited.Amount:0.00}",
            MoneyWithdrawn withdrawn => $"Amount: {withdrawn.Amount:0.00}",
            StateTransitionRejected rejected => $"{rejected.CommandType}: {rejected.Reason}",
            _ => @event.EventType
        };
    }
}

public sealed record AccountBalanceViewModel(
    bool IsOpen,
    decimal Balance,
    IReadOnlyList<TransactionHistoryItem> History);

public sealed record TransactionHistoryItem(
    int SequenceNumber,
    string EventType,
    string Description,
    DateTimeOffset CreatedAtUtc);
