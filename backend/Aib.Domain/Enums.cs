namespace Aib.Domain;

public enum UserStatus
{
    Invited,
    Active,
    Suspended,
    Disabled
}

public enum AccessLevel
{
    View,
    Manage,
    Billing
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

public enum ApprovalStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Invoiced
}

// ---- ClickUp integration ----

public enum ExternalConnectionStatus
{
    Active,
    Disabled,
    Error
}

/// <summary>ClickUp container hierarchy levels.</summary>
public enum ContainerType
{
    Workspace,
    Team,
    Space,
    Folder,
    List
}

public enum WorkItemType
{
    Task,
    Subtask
}

public enum ImportType
{
    Full,
    Incremental,
    Manual,
    Retry
}

public enum ImportStatus
{
    Queued,
    Running,
    Completed,
    CompletedWithErrors,
    Failed
}

/// <summary>Kind of external record an import touched (also used for sync cursors).</summary>
public enum ExternalEntityType
{
    Identity,
    Container,
    WorkItem,
    TimeEntry
}

public enum ImportAction
{
    Created,
    Updated,
    Unchanged,
    Failed,
    Skipped
}

public enum ImportRecordStatus
{
    Success,
    Failed,
    Skipped
}

// ---- External ↔ internal mapping ----

public enum MappingStatus
{
    Suggested,
    Confirmed,
    Ignored,
    Conflict,
    Unmapped
}

public enum MappingSource
{
    Manual,
    Rule,
    NameMatch,
    ParentMapping,
    ImportCreated
}
