namespace Aib.Domain;

public enum UserStatus
{
    Invited,
    Active,
    Suspended,
    Disabled
}

public enum AuthMethod
{
    Password,
    MagicLink,
    Google
}

public enum WorkStatus
{
    Pending,
    InProgress,
    Blocked,
    Completed,
    Cancelled,
    Archived
}

public enum BillingStatus
{
    NotReady,
    PendingReview,
    Ready,
    Finalized,
    Invoiced,
    Excluded
}

public enum BillingType
{
    Hourly,
    FixedFee,
    NonBillable
}

/// <summary>Rollup source selection for estimates and actual time.</summary>
public enum RollupMode
{
    Direct,
    Children,
    DirectAndChildren
}

/// <summary>How billing work is grouped into invoice lines.</summary>
public enum BillingRollupMode
{
    Detailed,
    Task,
    Parent,
    Project,
    Client
}

public enum ClientStatus
{
    Prospective,
    Active,
    Inactive,
    Archived
}

public enum ProjectStatus
{
    Planned,
    Active,
    OnHold,
    Completed,
    Archived
}

public enum MagicLinkPurpose
{
    Login,
    Invitation
}
