namespace Taskly.Modules.Tasks.Domain;

public static class TaskSortFields
{
    public static readonly IReadOnlyList<string> Allowed =
    [
        "name",
        "description",
        "priority",
        "due",
        "status",
        "createdAt",
        "updatedAt"
    ];

    public static bool IsAllowed(string field) => Allowed.Contains(field);
}
