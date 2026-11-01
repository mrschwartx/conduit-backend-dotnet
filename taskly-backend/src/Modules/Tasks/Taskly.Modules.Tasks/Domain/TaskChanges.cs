namespace Taskly.Modules.Tasks.Domain;

public sealed record TaskChanges(
    TaskName? Name,
    TaskDescription? Description,
    TaskPriority? Priority,
    DueDate? Due,
    TaskItemStatus? Status)
{
    public bool IsEmpty => Name is null && Description is null && Priority is null && Due is null && Status is null;
}
