using System.Text.RegularExpressions;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Domain;

public sealed record Email
{
    public const int MaxLength = 254;

    private static readonly Regex Pattern = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(object? raw)
    {
        if (raw is not string text)
        {
            throw new ValidationException("Email is required");
        }

        var value = TextRules.Trim(text);
        if (value.Length == 0 || value.Length > MaxLength || !Pattern.IsMatch(value))
        {
            throw new ValidationException("Email is invalid");
        }

        return new Email(value);
    }

    public static Email Restore(string value) => new(value);
}
