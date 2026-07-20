using Aib.Domain;

namespace Aib.Application.Integrations;

/// <summary>Builds ClickUp web app deep links from workspace + entity ids.</summary>
public static class ClickUpUrls
{
    public static string Task(string externalId) =>
        $"https://app.clickup.com/t/{Uri.EscapeDataString(externalId)}";

    public static string? Container(string? workspaceId, ContainerType type, string externalId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(externalId))
            return null;

        var team = Uri.EscapeDataString(workspaceId);
        var id = Uri.EscapeDataString(externalId);
        return type switch
        {
            ContainerType.List => $"https://app.clickup.com/{team}/v/li/{id}",
            ContainerType.Folder => $"https://app.clickup.com/{team}/v/f/{id}",
            ContainerType.Space => $"https://app.clickup.com/{team}/v/s/{id}",
            _ => null
        };
    }
}
