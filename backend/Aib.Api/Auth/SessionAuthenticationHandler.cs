using System.Security.Claims;
using System.Text.Encodings.Web;
using Aib.Application.Abstractions;
using Aib.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Aib.Api.Auth;

/// <summary>
/// Validates the opaque session cookie against the hashed token stored in
/// <c>user_session</c> and builds the caller's principal (id + role claims).
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionRepository sessions,
    IUserRepository users,
    ITokenService tokens,
    IClock clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(AuthConstants.SessionCookie, out var token) || string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var session = await sessions.GetActiveByHashAsync(tokens.Hash(token), Context.RequestAborted);
        if (session is null)
            return AuthenticateResult.Fail("Invalid session.");

        AppUser? user = await users.GetByIdAsync(session.UserId, Context.RequestAborted);
        if (user is null)
            return AuthenticateResult.Fail("User not found.");

        var roles = await users.GetRoleNamesAsync(user.Id, Context.RequestAborted);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, AuthConstants.Scheme);
        var principal = new ClaimsPrincipal(identity);

        await sessions.TouchAsync(session.Id, clock.UtcNow, Context.RequestAborted);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthConstants.Scheme));
    }
}
