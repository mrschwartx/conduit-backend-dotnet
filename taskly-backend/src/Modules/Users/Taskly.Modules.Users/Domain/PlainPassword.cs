using System.Text;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Domain;

public sealed record PlainPassword
{
    public const int MinLength = 8;

    public const int MaxBytes = 72;

    private PlainPassword(string value) => Value = value;

    public string Value { get; }

    public static PlainPassword Create(object? raw)
    {
        if (raw is not string text)
        {
            throw new ValidationException("Password is required");
        }

        if (text.Length < MinLength)
        {
            throw new ValidationException($"Password must be at least {MinLength} characters long");
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            throw new ValidationException($"Password must not exceed {MaxBytes} bytes");
        }

        return new PlainPassword(text);
    }

    public override string ToString() => "PlainPassword[value=***]";
}
