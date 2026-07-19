namespace Aib.Api.Auth;

public static class SessionCookie
{
    public static void Set(HttpContext ctx, string token, DateTimeOffset expiresAt, bool secure) =>
        ctx.Response.Cookies.Append(AuthConstants.SessionCookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt
        });

    public static void Clear(HttpContext ctx, bool secure) =>
        ctx.Response.Cookies.Append(AuthConstants.SessionCookie, "", new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch
        });
}
