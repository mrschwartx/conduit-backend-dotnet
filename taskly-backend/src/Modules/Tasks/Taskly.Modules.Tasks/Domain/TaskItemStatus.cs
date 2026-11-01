using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Domain;

public sealed class TaskItemStatus
{
    public static readonly TaskItemStatus Todo = new("todo");

    public static readonly TaskItemStatus InProgress = new("in_progress");

    public static readonly TaskItemStatus Done = new("done");

    private const string Label = "Task status";

    private static readonly TaskItemStatus[] All = [Todo, InProgress, Done];

    private TaskItemStatus(string value) => Value = value;

    public static TaskItemStatus Default => Todo;

    public string Value { get; }

    public static TaskItemStatus Create(object? raw)
    {
        var candidate = TextRules.Parse(raw, Label, 1, 50);
        return All.FirstOrDefault(status => status.Value == candidate)
               ?? throw new ValidationException($"{Label} must be one of: {AllowedValues()}");
    }

    public static TaskItemStatus Restore(string value) =>
        All.FirstOrDefault(status => status.Value == value)
        ?? throw new ArgumentException($"Unknown task status: {value}", nameof(value));

    private static string AllowedValues() => string.Join(", ", All.Select(status => status.Value));
}
