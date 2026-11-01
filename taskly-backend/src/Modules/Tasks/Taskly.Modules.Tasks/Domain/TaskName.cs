using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Domain;

public sealed record TaskName
{
    private const string Label = "Task name";

    private TaskName(string value) => Value = value;

    public string Value { get; }

    public static TaskName Create(object? raw) => new(TextRules.Parse(raw, Label, 1, 120));

    public static TaskName Restore(string value) => new(value);
}
