namespace Taskly.SharedKernel.Domain;

public static class TextRules
{
    public static string Trim(string value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && IsTrimmable(value[start]))
        {
            start++;
        }

        while (end > start && IsTrimmable(value[end - 1]))
        {
            end--;
        }

        return value.Substring(start, end - start);
    }

    public static string Parse(object? raw, string label, int minLength, int maxLength)
    {
        if (raw is not string text)
        {
            throw new ValidationException($"{label} is required");
        }

        var value = Trim(text);
        if (value.Length < minLength || value.Length > maxLength)
        {
            throw new ValidationException($"{label} must be between {minLength} and {maxLength} characters");
        }

        return value;
    }

    private static bool IsTrimmable(char character) =>
        char.IsWhiteSpace(character) || char.IsSeparator(character) || character == '\uFEFF';
}
