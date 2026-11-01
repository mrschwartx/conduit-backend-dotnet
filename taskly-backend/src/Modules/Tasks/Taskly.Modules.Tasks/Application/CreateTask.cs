using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Application;

public sealed class CreateTask(ITaskRepository tasks, TimeProvider clock)
{
    public async Task<TaskDto> ExecuteAsync(
        Guid ownerId,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var name = TaskName.Create(input.GetValueOrDefault("name"));
        var description = input.TryGetValue("description", out var rawDescription)
            ? TaskDescription.Create(rawDescription)
            : TaskDescription.Empty;
        var priority = TaskPriority.Create(input.GetValueOrDefault("priority"));
        var due = DueDate.Create(input.GetValueOrDefault("due"));
        var status = input.TryGetValue("status", out var rawStatus)
            ? TaskItemStatus.Create(rawStatus)
            : TaskItemStatus.Default;

        var task = TaskItem.Create(tasks.NextId(), ownerId, name, description, priority, due, status, Timestamps.Now(clock));
        await tasks.AddAsync(task, cancellationToken);
        return TaskDto.From(task);
    }
}
