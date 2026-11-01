using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Domain;

public sealed record TaskDescription
{
    private const string Label = "Task description";

    private TaskDescription(string value) => Value = value;

    public static TaskDescription Empty { get; } = new(string.Empty);

    public string Value { get; }

    public static TaskDescription Create(object? raw) => new(TextRules.Parse(raw, Label, 0, 2000));

    public static TaskDescription Restore(string value) => new(value);
}
