using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Domain;

public sealed class TaskPriority
{
    public static readonly TaskPriority Low = new("low");

    public static readonly TaskPriority Medium = new("medium");

    public static readonly TaskPriority High = new("high");

    private const string Label = "Task priority";

    private static readonly TaskPriority[] All = [Low, Medium, High];

    private TaskPriority(string value) => Value = value;

    public string Value { get; }

    public static TaskPriority Create(object? raw)
    {
        var candidate = TextRules.Parse(raw, Label, 1, 50);
        return All.FirstOrDefault(priority => priority.Value == candidate)
               ?? throw new ValidationException($"{Label} must be one of: {AllowedValues()}");
    }

    public static TaskPriority Restore(string value) =>
        All.FirstOrDefault(priority => priority.Value == value)
        ?? throw new ArgumentException($"Unknown task priority: {value}", nameof(value));

    private static string AllowedValues() => string.Join(", ", All.Select(priority => priority.Value));
}
