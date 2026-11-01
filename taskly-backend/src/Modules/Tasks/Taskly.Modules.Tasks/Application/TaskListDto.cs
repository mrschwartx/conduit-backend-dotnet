namespace Taskly.Modules.Tasks.Application;

public sealed record TaskListDto(IReadOnlyList<TaskDto> Tasks, long TaskCount);
