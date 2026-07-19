namespace Aib.Api.Auth;

public static class AuthConstants
{
    public const string Scheme = "Session";
    public const string SessionCookie = "aib_session";
    public const string CsrfCookie = "aib_csrf";
    public const string CsrfHeader = "X-CSRF-Token";
}
