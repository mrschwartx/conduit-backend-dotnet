namespace Taskly.Modules.Tasks.Domain;

public sealed class TaskItem
{
    private TaskItem(
        Guid id,
        Guid ownerId,
        TaskName name,
        TaskDescription description,
        TaskPriority priority,
        DueDate due,
        TaskItemStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        OwnerId = ownerId;
        Name = name;
        Description = description;
        Priority = priority;
        Due = due;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public Guid OwnerId { get; }

    public TaskName Name { get; private set; }

    public TaskDescription Description { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DueDate Due { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TaskItem Create(
        Guid id,
        Guid ownerId,
        TaskName name,
        TaskDescription description,
        TaskPriority priority,
        DueDate due,
        TaskItemStatus status,
        DateTimeOffset now) =>
        new(id, ownerId, name, description, priority, due, status, now, now);

    public static TaskItem Restore(
        Guid id,
        Guid ownerId,
        TaskName name,
        TaskDescription description,
        TaskPriority priority,
        DueDate due,
        TaskItemStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id, ownerId, name, description, priority, due, status, createdAt, updatedAt);

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;

    public void Update(TaskChanges changes, DateTimeOffset now)
    {
        Name = changes.Name ?? Name;
        Description = changes.Description ?? Description;
        Priority = changes.Priority ?? Priority;
        Due = changes.Due ?? Due;
        Status = changes.Status ?? Status;
        UpdatedAt = now;
    }
}
