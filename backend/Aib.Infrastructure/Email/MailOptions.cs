namespace Aib.Infrastructure.Email;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    /// <summary>null / none / "" = no encryption; "starttls" or "ssl" when required.</summary>
    public string? Encryption { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "noreply@localhost";
    public string FromName { get; set; } = "Agency Billing Console";
}
