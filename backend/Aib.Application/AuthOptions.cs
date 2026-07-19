namespace Aib.Application;

public sealed class AuthOptions
{
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Magic-link lifetime. Spec requires exactly one hour.</summary>
    public TimeSpan MagicLinkLifetime { get; set; } = TimeSpan.FromHours(1);

    public int MagicLinkMaxPerHour { get; set; } = 5;

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Base URL used to build the magic-link consume URL in emails.</summary>
    public string AppBaseUrl { get; set; } = "https://localhost:3000";
}
