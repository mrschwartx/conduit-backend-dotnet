using System.Globalization;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Domain;

public sealed record DueDate
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd",
        "r",
        "ddd, d MMM yyyy HH:mm:ss 'GMT'"
    ];

    private DueDate(DateTimeOffset value) => Value = value;

    public DateTimeOffset Value { get; }

    public static DueDate Create(object? raw)
    {
        if (raw is not string text || TextRules.Trim(text).Length == 0)
        {
            throw new ValidationException("Task due date is required");
        }

        var parsed = DateTimeOffset.TryParseExact(
            TextRules.Trim(text),
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value);
        if (!parsed)
        {
            throw new ValidationException("Task due date is invalid");
        }

        return new DueDate(Timestamps.Truncate(value));
    }

    public static DueDate Restore(DateTimeOffset value) => new(value);
}
