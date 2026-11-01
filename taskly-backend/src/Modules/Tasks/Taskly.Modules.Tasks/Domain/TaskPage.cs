namespace Taskly.Modules.Tasks.Domain;

public sealed record TaskPage(IReadOnlyList<TaskItem> Tasks, long Total);
