using Aib.Domain.Entities;

namespace Aib.Domain;

/// <summary>
/// Pure business rules for the task hierarchy. Data-dependent checks (circular
/// references) accept the already-resolved ancestor set so the domain stays
/// free of persistence concerns.
/// </summary>
public static class TaskRules
{
    /// <summary>Validate a task's client/project/parent relationship.</summary>
    public static void ValidatePlacement(WorkTask task, WorkTask? parent, Project? project)
    {
        if (task.ClientId == Guid.Empty)
            throw new DomainException("A task must belong to a client.");

        if (project is not null && project.ClientId != task.ClientId)
            throw new DomainException("The task's project must belong to the same client as the task.");

        if (parent is not null)
        {
            if (parent.ClientId != task.ClientId)
                throw new DomainException("A child task must belong to the same client as its parent.");

            // Standalone parent (no project) => descendants must also be standalone.
            if (parent.ProjectId is null && task.ProjectId is not null)
                throw new DomainException("A subtask of a standalone task cannot belong to a project.");

            // Otherwise a child should share the parent's project.
            if (parent.ProjectId is not null && task.ProjectId != parent.ProjectId)
                throw new DomainException("A subtask must belong to the same project as its parent task.");
        }
    }

    /// <summary>
    /// Prevent circular parent relationships. <paramref name="ancestorIds"/> is the
    /// set of ids on the path from the proposed parent up to the root.
    /// </summary>
    public static void EnsureNoCycle(Guid taskId, Guid? parentTaskId, IReadOnlySet<Guid> ancestorIds)
    {
        if (parentTaskId is null)
            return;

        if (parentTaskId == taskId || ancestorIds.Contains(taskId))
            throw new DomainException("A task cannot be its own ancestor (circular relationship).");
    }
}
