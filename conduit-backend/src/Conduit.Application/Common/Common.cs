using System.Globalization;
using System.Text.Json;
using FluentValidation;

namespace Conduit.Application.Common;

public sealed class ApiException : Exception
{
    public int Status { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ApiException(int status, IReadOnlyDictionary<string, string[]> errors) : base($"HTTP {status}")
    { Status = status; Errors = errors; }

    public ApiException(int status, string key, string message)
        : this(status, new Dictionary<string, string[]> { [key] = [message] }) { }
}

/// <summary>Distinguishes "absent" (HasValue=false) from "present, possibly null".</summary>
public readonly record struct Optional<T>(bool HasValue, T? Value)
{
    public static Optional<T> Of(T? value) => new(true, value);
}

public sealed class FieldReader(JsonElement obj)
{
    private readonly Dictionary<string, string[]> _errors = new();
    public IReadOnlyDictionary<string, string[]> Errors => _errors;

    public Optional<string?> String(string name, bool trim = true)
    {
        if (!obj.TryGetProperty(name, out var v)) return default;
        switch (v.ValueKind)
        {
            case JsonValueKind.Null: return Optional<string?>.Of(null);
            case JsonValueKind.String:
                var s = v.GetString()!;
                return Optional<string?>.Of(trim ? s.Trim() : s);
            default:
                _errors[name] = ["is invalid"];
                return default;
        }
    }

    public Optional<List<string>?> StringList(string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return default;
        if (v.ValueKind == JsonValueKind.Null) return Optional<List<string>?>.Of(null);
        if (v.ValueKind != JsonValueKind.Array) { _errors[name] = ["is invalid"]; return default; }
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) { _errors[name] = ["is invalid"]; return default; }
            var t = item.GetString()!.Trim();
            if (seen.Add(t)) list.Add(t);   // de-duplicate, keep first-seen order
        }
        return Optional<List<string>?>.Of(list);
    }
}

public static class Validate
{
    /// <summary>Runs the validator and throws one 422 holding every failing field.</summary>
    public static void Run<T>(IValidator<T> validator, T input, FieldReader? reader = null)
    {
        var errors = new Dictionary<string, List<string>>();
        if (reader is not null)
            foreach (var (k, v) in reader.Errors) errors[k] = [.. v];
        foreach (var f in validator.Validate(input).Errors)
        {
            if (reader?.Errors.ContainsKey(f.PropertyName) == true) continue;   // type error already reported
            if (!errors.TryGetValue(f.PropertyName, out var list)) errors[f.PropertyName] = list = [];
            if (!list.Contains(f.ErrorMessage)) list.Add(f.ErrorMessage);
        }
        if (errors.Count > 0)
            throw new ApiException(422, errors.ToDictionary(k => k.Key, k => k.Value.ToArray()));
    }
}

public readonly record struct Page(int Offset, int Limit);

public static class Paging
{
    public static Page Parse(string? offset, string? limit)
    {
        var errors = new Dictionary<string, string[]>();
        long off = 0, lim = 20;
        if (offset is not null)
        {
            if (!long.TryParse(offset, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out off)) errors["offset"] = ["is invalid"];
            else if (off < 0) errors["offset"] = ["must be greater than or equal to 0"];
        }
        if (limit is not null)
        {
            if (!long.TryParse(limit, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out lim)) errors["limit"] = ["is invalid"];
            else if (lim < 1) errors["limit"] = ["must be greater than or equal to 1"];
        }
        if (errors.Count > 0) throw new ApiException(422, errors);
        return new Page((int)Math.Min(off, int.MaxValue), (int)Math.Min(lim, 50));
    }
}

/// <summary>Character-based (Unicode scalar) FluentValidation rules with contract messages.</summary>
internal static class Rules
{
    private static int Len(string s) => s.EnumerateRunes().Count();

    public static IRuleBuilderOptions<T, string?> Required<T>(this IRuleBuilder<T, string?> r) =>
        r.Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("can't be blank");

    public static IRuleBuilderOptions<T, string?> MinChars<T>(this IRuleBuilder<T, string?> r, int n) =>
        r.Must(s => s is null || Len(s) >= n).WithMessage($"is too short (minimum is {n} characters)");

    public static IRuleBuilderOptions<T, string?> MaxChars<T>(this IRuleBuilder<T, string?> r, int n) =>
        r.Must(s => s is null || Len(s) <= n).WithMessage($"is too long (maximum is {n} characters)");

    public static IRuleBuilderOptions<T, string?> Pattern<T>(this IRuleBuilder<T, string?> r, System.Text.RegularExpressions.Regex rx) =>
        r.Must(s => s is null || rx.IsMatch(s)).WithMessage("is invalid");

    public static IRuleBuilderOptions<T, string?> HttpsUrlOrEmpty<T>(this IRuleBuilder<T, string?> r) =>
        r.Must(s => string.IsNullOrEmpty(s) ||
                    (Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host.Length > 0))
         .WithMessage("is invalid");

    public static IRuleBuilderOptions<T, string?> Named<T>(this IRuleBuilderOptions<T, string?> r, string name) =>
        r.OverridePropertyName(name);
}
