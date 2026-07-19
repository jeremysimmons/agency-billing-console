using Aib.Api.Auth;
using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, IOptions<WebOptions> web) : ControllerBase
{
    private bool Secure => web.Value.SecureCookies;

    /// <summary>Ensures a CSRF cookie is issued and returns nothing sensitive.</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf() => Ok(new { ok = true });

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginWithPasswordAsync(request, ct);
        return Complete(result);
    }

    [HttpPost("magic-link/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestMagicLink([FromBody] MagicLinkRequest request, CancellationToken ct)
    {
        await auth.RequestMagicLinkAsync(request, ct);
        // Always identical response regardless of whether the email exists.
        return Ok(new { message = "If the email matches an account, a sign-in link has been sent." });
    }

    [HttpPost("magic-link/consume")]
    [AllowAnonymous]
    public async Task<IActionResult> ConsumeMagicLink([FromBody] ConsumeTokenRequest request, CancellationToken ct)
    {
        var result = await auth.ConsumeMagicLinkAsync(request.Token, ct);
        return Complete(result);
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> Google([FromBody] GoogleLinkRequest request, CancellationToken ct)
    {
        var result = await auth.AuthenticateWithGoogleAsync(request, ct);
        return Complete(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await auth.GetCurrentAsync(userId, ct);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(AuthConstants.SessionCookie, out var token) && !string.IsNullOrEmpty(token))
            await auth.LogoutAsync(token, ct);
        SessionCookie.Clear(HttpContext, Secure);
        return Ok(new { ok = true });
    }

    private IActionResult Complete(AuthResult result)
    {
        if (!result.Succeeded || result.User is null || result.SessionToken is null)
            return Unauthorized(new { error = result.Error ?? "Authentication failed." });

        SessionCookie.Set(HttpContext, result.SessionToken, result.ExpiresAt!.Value, Secure);
        return Ok(result.User);
    }
}

public sealed record ConsumeTokenRequest(string Token);
