namespace EventSourcingBankAccountWeb.Infrastructure;

/// <summary>
/// Signs the serialized representation of an event and verifies it before use.
/// </summary>
public interface IEventDataSigner
{
    string Algorithm { get; }
    string Sign(string data);
    bool Verify(string data, string signature);
}
