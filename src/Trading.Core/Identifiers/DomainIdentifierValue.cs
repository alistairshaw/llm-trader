using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace Trading.Core.Identifiers;

internal readonly record struct DomainIdentifierValue
{
    private const int CanonicalLength = 26;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly BigInteger MaximumValue = BigInteger.One << 128;

    private DomainIdentifierValue(string value)
    {
        Value = value;
    }

    private string Value { get; }

    internal static DomainIdentifierValue New()
    {
        Span<byte> bytes = stackalloc byte[16];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(timestamp >> 40);
        bytes[1] = (byte)(timestamp >> 32);
        bytes[2] = (byte)(timestamp >> 24);
        bytes[3] = (byte)(timestamp >> 16);
        bytes[4] = (byte)(timestamp >> 8);
        bytes[5] = (byte)timestamp;
        RandomNumberGenerator.Fill(bytes[6..]);

        return new DomainIdentifierValue(Encode(bytes));
    }

    internal static DomainIdentifierValue Parse(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Length != CanonicalLength)
        {
            throw new ArgumentException(
                $"A domain identifier must contain exactly {CanonicalLength.ToString(CultureInfo.InvariantCulture)} characters.",
                parameterName);
        }

        BigInteger decoded = BigInteger.Zero;
        Span<char> canonical = stackalloc char[CanonicalLength];

        for (var index = 0; index < value.Length; index++)
        {
            var character = char.ToUpperInvariant(value[index]);
            var digit = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (digit < 0)
            {
                throw new ArgumentException(
                    $"A domain identifier may contain only canonical Crockford Base32 characters from {Alphabet}.",
                    parameterName);
            }

            canonical[index] = character;
            decoded = (decoded * 32) + digit;
        }

        if (decoded.IsZero)
        {
            throw new ArgumentException("An empty domain identifier is not valid.", parameterName);
        }

        if (decoded >= MaximumValue)
        {
            throw new ArgumentException("The domain identifier exceeds the 128-bit ULID range.", parameterName);
        }

        return new DomainIdentifierValue(new string(canonical));
    }

    public override string ToString() => Value;

    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        Span<char> encoded = stackalloc char[CanonicalLength];

        for (var index = CanonicalLength - 1; index >= 0; index--)
        {
            value = BigInteger.DivRem(value, 32, out var remainder);
            encoded[index] = Alphabet[(int)remainder];
        }

        return new string(encoded);
    }
}
