namespace Aib.Domain;

public static class SharedClients
{
    public const string Name = "Shared";

    public static bool IsShared(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && string.Equals(name.Trim(), Name, StringComparison.OrdinalIgnoreCase);
}
