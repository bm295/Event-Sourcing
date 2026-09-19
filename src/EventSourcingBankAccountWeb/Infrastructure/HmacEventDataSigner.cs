using System.Security.Cryptography;
using System.Text;

namespace EventSourcingBankAccountWeb.Infrastructure;

public sealed class HmacEventDataSigner : IEventDataSigner
{
    private readonly byte[] _key;

    public HmacEventDataSigner(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < 32)
        {
            throw new ArgumentException("The event signing key must be at least 256 bits.", nameof(key));
        }

        _key = key.ToArray();
    }

    public string Algorithm => "HMAC-SHA256";

    public string Sign(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(data)));
    }

    public bool Verify(string data, string signature)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(signature);

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(data));
        return CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature);
    }
}
