using Taskly.Modules.Tasks.Application;
using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.UnitTests.Support;

public sealed class InMemoryTaskRepository : ITaskRepository
{
    public Dictionary<Guid, TaskItem> Tasks { get; } = new();

    public Guid NextId() => EntityIds.Generate();

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        Tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        if (!Tasks.ContainsKey(task.Id))
        {
            return Task.FromResult(false);
        }

        Tasks[task.Id] = task;
        return Task.FromResult(true);
    }

    public Task<TaskItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tasks.GetValueOrDefault(id));

    public Task<TaskPage> FindByOwnerAsync(
        Guid ownerId,
        string? status,
        string? orderBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var owned = Tasks.Values
            .Where(task => task.OwnerId == ownerId && (status is null || task.Status.Value == status))
            .ToList();

        IOrderedEnumerable<TaskItem> ordered = orderBy switch
        {
            "name" => owned.OrderBy(task => task.Name.Value, StringComparer.Ordinal),
            "priority" => owned.OrderBy(task => task.Priority.Value, StringComparer.Ordinal),
            "status" => owned.OrderBy(task => task.Status.Value, StringComparer.Ordinal),
            "due" => owned.OrderBy(task => task.Due.Value),
            _ => owned.OrderBy(task => task.CreatedAt)
        };

        var items = ordered
            .ThenBy(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult(new TaskPage(items, owned.Count));
    }

    public Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tasks.Remove(id));

    public Task<int> DeleteByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var ids = Tasks.Values.Where(task => task.OwnerId == ownerId).Select(task => task.Id).ToList();
        foreach (var id in ids)
        {
            Tasks.Remove(id);
        }

        return Task.FromResult(ids.Count);
    }
}

public sealed class TasksFixture
{
    public InMemoryTaskRepository Tasks { get; } = new();

    public FixedTimeProvider Clock { get; } = new(Ids.FixedNow);

    public CreateTask Create => new(Tasks, Clock);

    public GetTask Get => new(Tasks);

    public ListTasksByUser List => new(Tasks);

    public UpdateTask Update => new(Tasks, Clock);

    public DeleteTask Delete => new(Tasks);

    public DeleteTasksByOwner DeleteByOwner => new(Tasks);

    public static IReadOnlyDictionary<string, object?> Payload(
        string? name = "Read Atomic Habits",
        string? description = "Finish reading Atomic Habits by James Clear",
        string? priority = "low",
        string? due = "2026-12-08T10:00:00Z",
        string? status = "todo")
    {
        var payload = new Dictionary<string, object?>();
        if (name is not null) payload["name"] = name;
        if (description is not null) payload["description"] = description;
        if (priority is not null) payload["priority"] = priority;
        if (due is not null) payload["due"] = due;
        if (status is not null) payload["status"] = status;
        return payload;
    }

    public async Task<TaskDto> MakeAsync(Guid? owner = null, string name = "Read Atomic Habits", string status = "todo")
    {
        Clock.Advance(TimeSpan.FromSeconds(1));
        return await Create.ExecuteAsync(owner ?? Ids.User, Payload(name: name, status: status));
    }
}
