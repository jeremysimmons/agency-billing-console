namespace Aib.Application.Integrations;

/// <summary>
/// Parses ClickUp folder titles into client Code + Name while preserving the original.
/// Example: "AWA_BCR - Black Canyon Rentals" → Code=AWA_BCR, Name=Black Canyon Rentals.
/// </summary>
public static class ClickUpFolderNaming
{
    public static (string Name, string? Code, string OriginalName) Parse(string raw)
    {
        var original = (raw ?? string.Empty).Trim();
        if (original.Length == 0)
            return (string.Empty, null, string.Empty);

        var dash = original.IndexOf('-');
        if (dash < 0)
            return (original, null, original);

        var code = original[..dash].Trim();
        var name = original[(dash + 1)..].Trim();
        if (name.Length == 0)
            return (original, string.IsNullOrEmpty(code) ? null : code, original);

        return (name, string.IsNullOrEmpty(code) ? null : code, original);
    }
}
