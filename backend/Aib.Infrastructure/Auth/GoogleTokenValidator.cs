using Aib.Application.Abstractions;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Aib.Infrastructure.Auth;

public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string? HostedDomain { get; set; }
}

public sealed class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings();
        if (!string.IsNullOrWhiteSpace(_options.ClientId))
            settings.Audience = new[] { _options.ClientId };

        // Throws InvalidJwtException on bad signature / audience / issuer / expiry.
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

        return new GoogleIdentity(
            Subject: payload.Subject,
            Email: payload.Email ?? string.Empty,
            EmailVerified: payload.EmailVerified,
            HostedDomain: payload.HostedDomain,
            Name: payload.Name);
    }
}
