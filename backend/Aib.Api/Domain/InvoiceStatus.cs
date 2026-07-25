namespace Aib.Domain;

/// <summary>Invoice workflow status as named instances (not a CLR enum).</summary>
public sealed class InvoiceStatus : IEquatable<InvoiceStatus>
{
    public static readonly InvoiceStatus Preparing = new("preparing");
    public static readonly InvoiceStatus Sent = new("sent");
    public static readonly InvoiceStatus PartiallyPaid = new("partially-paid");
    public static readonly InvoiceStatus FullyPaid = new("fully-paid");

    public static IReadOnlyList<InvoiceStatus> All { get; } =
        [Preparing, Sent, PartiallyPaid, FullyPaid];

    public string Value { get; }

    private InvoiceStatus(string value) => Value = value;

    public static bool TryParse(string? value, out InvoiceStatus status)
    {
        var key = (value ?? "").Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
        switch (key)
        {
            case "preparing":
            case "open":
            case "":
                status = Preparing;
                return true;
            case "sent":
                status = Sent;
                return true;
            case "partially-paid":
            case "partiallypaid":
                status = PartiallyPaid;
                return true;
            case "fully-paid":
            case "fullypaid":
            case "closed":
                status = FullyPaid;
                return true;
            default:
                status = Preparing;
                return false;
        }
    }

    public static InvoiceStatus Parse(string? value)
    {
        _ = TryParse(value, out var status);
        return status;
    }

    public override string ToString() => Value;

    public bool Equals(InvoiceStatus? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is InvoiceStatus other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(InvoiceStatus? left, InvoiceStatus? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    public static bool operator !=(InvoiceStatus? left, InvoiceStatus? right) => !(left == right);
}
