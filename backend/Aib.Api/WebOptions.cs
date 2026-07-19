namespace Aib.Api;

public sealed class WebOptions
{
    /// <summary>Emit Secure cookies. True in production (behind TLS).</summary>
    public bool SecureCookies { get; set; } = true;
}
