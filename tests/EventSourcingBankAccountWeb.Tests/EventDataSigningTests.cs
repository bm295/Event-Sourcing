using EventSourcingBankAccountWeb.Domain;
using EventSourcingBankAccountWeb.Infrastructure;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class EventDataSigningTests
{
    [Fact]
    public void Appended_event_has_a_verifiable_data_signature()
    {
        var signer = new HmacEventDataSigner(Enumerable.Repeat((byte)42, 32).ToArray());
        var store = new InMemoryEventStore(signer);
        var domainEvent = new AccountOpened(
            "event-1", "account-1", 1, "correlation-1", "command-1",
            DateTimeOffset.Parse("2026-09-19T12:00:00Z"), "Demo User");

        var record = store.Append(domainEvent, 0);

        Assert.Equal("HMAC-SHA256", record.SignatureAlgorithm);
        Assert.True(record.SignatureValid);
        Assert.True(signer.Verify(record.PayloadJson, record.DataSignature));
        Assert.False(signer.Verify(record.PayloadJson + " ", record.DataSignature));
    }

    [Fact]
    public void Signer_rejects_keys_shorter_than_256_bits()
    {
        var exception = Assert.Throws<ArgumentException>(() => new HmacEventDataSigner(new byte[31]));

        Assert.Contains("at least 256 bits", exception.Message);
    }
}
