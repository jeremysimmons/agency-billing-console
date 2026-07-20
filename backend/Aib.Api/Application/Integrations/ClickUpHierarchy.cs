namespace Aib.Application.Integrations;

/// <summary>ClickUp container type strings (API v3 / UI names) used in hierarchy rows.</summary>
public static class ClickUpHierarchyTypes
{
    public const string Workspace = "workspace";
    public const string Space = "space";
    public const string Folder = "folder";
    public const string List = "list";
}

/// <summary>
/// One row of the ClickUp container hierarchy (space / folder / list),
/// matching <c>import/clickup-hierarchy.csv</c>: type, id, name, parent_type, parent_id.
/// </summary>
public sealed record ClickUpHierarchyNode(
    string Type,
    string Id,
    string Name,
    string ParentType,
    string ParentId);

/// <summary>Builds a flat, top-down ClickUp space → folder → list hierarchy for a workspace.</summary>
public interface IClickUpHierarchyBuilder
{
    /// <summary>
    /// Walk spaces, folders, and lists for the given team (workspace).
    /// When <paramref name="teamId"/> is null, uses <see cref="Aib.Application.ClickUpOptions.TeamId"/>.
    /// </summary>
    Task<IReadOnlyList<ClickUpHierarchyNode>> BuildAsync(
        string? teamId = null, CancellationToken ct = default);
}
