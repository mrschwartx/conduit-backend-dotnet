namespace Taskly.Modules.Tasks.Domain;

public interface ITaskRepository
{
    Guid NextId();

    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task<TaskItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TaskPage> FindByOwnerAsync(
        Guid ownerId,
        string? status,
        string? orderBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> DeleteByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
}
