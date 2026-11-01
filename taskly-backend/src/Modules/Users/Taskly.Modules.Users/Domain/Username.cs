using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Domain;

public sealed record Username
{
    public const int MinLength = 3;

    public const int MaxLength = 50;

    private Username(string value) => Value = value;

    public string Value { get; }

    public static Username Create(object? raw)
    {
        if (raw is not string)
        {
            throw new ValidationException("Username is required");
        }

        return new Username(TextRules.Parse(raw, "Username", MinLength, MaxLength));
    }

    public static Username Restore(string value) => new(value);
}
