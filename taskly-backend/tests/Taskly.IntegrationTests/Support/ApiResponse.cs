using System.Text.Json;

namespace Taskly.IntegrationTests.Support;

public sealed class ApiResponse(
    int status,
    string body,
    IReadOnlyDictionary<string, string> headers,
    IReadOnlyList<string> setCookies)
{
    public int Status { get; } = status;

    public string Body { get; } = body;

    public IReadOnlyList<string> SetCookies { get; } = setCookies;

    public JsonElement Json => JsonDocument.Parse(Body).RootElement;

    public string? Header(string name) => headers.TryGetValue(name, out var value) ? value : null;

    public string Text(string property) => Json.GetProperty(property).GetString()!;
}
