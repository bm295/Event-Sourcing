namespace EventSourcingBankAccountWeb.Domain;

public abstract record BankAccountCommand(string AccountId);

public sealed record OpenAccount(string AccountId, string OwnerName) : BankAccountCommand(AccountId);

public sealed record DepositMoney(string AccountId, decimal Amount) : BankAccountCommand(AccountId);

public sealed record WithdrawMoney(string AccountId, decimal Amount) : BankAccountCommand(AccountId);
