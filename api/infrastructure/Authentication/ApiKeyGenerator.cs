using System.Security.Cryptography;
using System.Text;

namespace api.Infrastructure.Authentication;

// Shared by KeysController (create/rollover) and ApiKeyAuthenticationHandler (lookup), so hashing can never drift.
public static class ApiKeyGenerator
{
    private const string KeyPrefix = "ak_";
    private const int PreviewLength = 12;

    public static string GenerateKey() =>
        KeyPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static string Preview(string key) =>
        key[..Math.Min(PreviewLength, key.Length)] + "…";
}
