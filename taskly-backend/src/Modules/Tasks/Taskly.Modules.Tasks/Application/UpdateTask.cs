using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Application;

public sealed class UpdateTask(ITaskRepository tasks, TimeProvider clock)
{
    public async Task<TaskDto> ExecuteAsync(
        Guid requesterId,
        string taskId,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken = default)
    {
        var id = EntityIds.Parse(taskId, "task");
        var task = await tasks.FindByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("Task not found!");

        if (!task.IsOwnedBy(requesterId))
        {
            throw new ForbiddenException("You are not authorized to update this task");
        }

        task.Update(ParseChanges(changes), Timestamps.Now(clock));

        if (!await tasks.UpdateAsync(task, cancellationToken))
        {
            throw new NotFoundException("Task not found!");
        }

        return TaskDto.From(task);
    }

    private static TaskChanges ParseChanges(IReadOnlyDictionary<string, object?> source)
    {
        var parsed = new TaskChanges(
            source.TryGetValue("name", out var name) ? TaskName.Create(name) : null,
            source.TryGetValue("description", out var description) ? TaskDescription.Create(description) : null,
            source.TryGetValue("priority", out var priority) ? TaskPriority.Create(priority) : null,
            source.TryGetValue("due", out var due) ? DueDate.Create(due) : null,
            source.TryGetValue("status", out var status) ? TaskItemStatus.Create(status) : null);

        if (parsed.IsEmpty)
        {
            throw new ValidationException("No updatable fields provided");
        }

        return parsed;
    }
}
