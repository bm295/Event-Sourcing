namespace EventSourcingBankAccountWeb.Domain;

public sealed class AccountBalanceProjection
{
    public AccountBalanceViewModel Build(IEnumerable<BankAccountEvent> events)
    {
        var account = new BankAccount();
        var orderedEvents = events.OrderBy(e => e.SequenceNumber).ToList();
        account.LoadFromHistory(orderedEvents);

        var history = orderedEvents
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
