using System.Text.Json.Serialization;
using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Application;

public sealed record TaskDto(
    [property: JsonPropertyName("_id")] string Id,
    string Name,
    string Description,
    string Priority,
    string Due,
    string Status,
    string Owner,
    string CreatedAt,
    string UpdatedAt)
{
    public static TaskDto From(TaskItem task) => new(
        task.Id.ToString(),
        task.Name.Value,
        task.Description.Value,
        task.Priority.Value,
        Timestamps.Format(task.Due.Value),
        task.Status.Value,
        task.OwnerId.ToString(),
        Timestamps.Format(task.CreatedAt),
        Timestamps.Format(task.UpdatedAt));
}
