using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Application;

public sealed class DeleteTask(ITaskRepository tasks)
{
    public async Task ExecuteAsync(Guid requesterId, string taskId, CancellationToken cancellationToken = default)
    {
        var id = EntityIds.Parse(taskId, "task");
        var task = await tasks.FindByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("Task not found!");

        if (!task.IsOwnedBy(requesterId))
        {
            throw new ForbiddenException("You are not authorized to delete this task");
        }

        if (!await tasks.DeleteByIdAsync(id, cancellationToken))
        {
            throw new NotFoundException("Task not found!");
        }
    }
}
