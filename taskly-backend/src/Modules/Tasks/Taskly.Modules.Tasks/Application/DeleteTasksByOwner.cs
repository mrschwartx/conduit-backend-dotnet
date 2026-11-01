using Taskly.Modules.Tasks.Domain;

namespace Taskly.Modules.Tasks.Application;

public sealed class DeleteTasksByOwner(ITaskRepository tasks)
{
    public Task<int> ExecuteAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        tasks.DeleteByOwnerAsync(ownerId, cancellationToken);
}
