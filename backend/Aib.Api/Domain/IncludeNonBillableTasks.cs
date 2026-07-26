namespace Aib.Domain;

/// <summary>How non-billable tasks are included on an invoice (not a CLR enum).</summary>
public sealed class IncludeNonBillableTasks : IEquatable<IncludeNonBillableTasks>
{
    public static readonly IncludeNonBillableTasks None = new("none");
    public static readonly IncludeNonBillableTasks Detail = new("detail");
    public static readonly IncludeNonBillableTasks Summary = new("summary");

    public static IReadOnlyList<IncludeNonBillableTasks> All { get; } =
        [None, Detail, Summary];

    public string Value { get; }

    private IncludeNonBillableTasks(string value) => Value = value;

    public static bool TryParse(string? value, out IncludeNonBillableTasks mode)
    {
        var key = (value ?? "").Trim().ToLowerInvariant();
        switch (key)
        {
            case "":
            case "none":
                mode = None;
                return true;
            case "detail":
                mode = Detail;
                return true;
            case "summary":
                mode = Summary;
                return true;
            default:
                mode = None;
                return false;
        }
    }

    public static IncludeNonBillableTasks Parse(string? value)
    {
        _ = TryParse(value, out var mode);
        return mode;
    }

    public override string ToString() => Value;

    public bool Equals(IncludeNonBillableTasks? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is IncludeNonBillableTasks other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(IncludeNonBillableTasks? left, IncludeNonBillableTasks? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    public static bool operator !=(IncludeNonBillableTasks? left, IncludeNonBillableTasks? right) => !(left == right);
}
