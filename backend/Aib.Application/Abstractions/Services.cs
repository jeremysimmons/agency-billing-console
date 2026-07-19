using Aib.Domain;

namespace Aib.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Generates opaque secrets and hashes them for at-rest storage.</summary>
public interface ITokenService
{
    /// <summary>Create a URL-safe random token (the plaintext handed to the user).</summary>
    string CreateToken();

    /// <summary>Deterministic hash (SHA-256) used to look up / store a token.</summary>
    string Hash(string token);
}

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

public sealed record GoogleIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string? HostedDomain,
    string? Name);

public interface IGoogleTokenValidator
{
    /// <summary>Validate a Google ID token against the configured client id / issuer.</summary>
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default);
}

/// <summary>Ambient information about the authenticated caller.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsContractorSide { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
