namespace Aib.Domain;

/// <summary>Canonical role names. Seeded on startup. Users are agency or contractor.</summary>
public static class Roles
{
    public const string Agency = "agency";
    public const string Contractor = "contractor";

    public static readonly IReadOnlyList<string> All = [Agency, Contractor];
}
