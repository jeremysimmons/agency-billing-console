using System.Security.Cryptography;

namespace Aib.Api.Auth;

/// <summary>
/// Double-submit CSRF protection. Safe methods ensure a non-httpOnly csrf cookie
/// exists; unsafe methods require the <c>X-CSRF-Token</c> header to match it.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next, bool secureCookies)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        var cookie = context.Request.Cookies[AuthConstants.CsrfCookie];

        if (SafeMethods.Contains(context.Request.Method))
        {
            if (string.IsNullOrEmpty(cookie))
                IssueCsrfCookie(context);
            await next(context);
            return;
        }

        var header = context.Request.Headers[AuthConstants.CsrfHeader].ToString();
        if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(cookie),
                System.Text.Encoding.UTF8.GetBytes(header)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid CSRF token." });
            return;
        }

        await next(context);
    }

    private void IssueCsrfCookie(HttpContext context)
    {
        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        context.Response.Cookies.Append(AuthConstants.CsrfCookie, value, new CookieOptions
        {
            HttpOnly = false,
            Secure = secureCookies,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }
}
