using System.Security.Cryptography;
using Aib.Application.Abstractions;

namespace Aib.Infrastructure.Security;

/// <summary>Creates high-entropy URL-safe tokens and SHA-256 hashes for at-rest storage.</summary>
public sealed class TokenService : ITokenService
{
    private const int TokenBytes = 32;

    public string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        return Base64Url(bytes);
    }

    public string Hash(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
