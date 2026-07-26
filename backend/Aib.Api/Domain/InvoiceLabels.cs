namespace Aib.Domain;

public static class InvoiceLabels
{
    public const string None = "none";

    public static bool IsNone(string? label) =>
        string.Equals(label?.Trim(), None, StringComparison.OrdinalIgnoreCase);
}
