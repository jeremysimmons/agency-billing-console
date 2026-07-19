namespace Aib.Domain;

/// <summary>Canonical role names. Seeded on startup.</summary>
public static class Roles
{
    public const string ContractorAdmin = "contractor_admin";
    public const string Contractor = "contractor";
    public const string AgencyAdmin = "agency_admin";
    public const string AgencyManager = "agency_manager";
    public const string AgencyViewer = "agency_viewer";
    public const string BillingViewer = "billing_viewer";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ContractorAdmin,
        Contractor,
        AgencyAdmin,
        AgencyManager,
        AgencyViewer,
        BillingViewer
    };

    /// <summary>Roles that manage the contractor side (full access).</summary>
    public static readonly IReadOnlyList<string> ContractorSide = new[]
    {
        ContractorAdmin,
        Contractor
    };
}
