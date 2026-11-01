using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Taskly.IntegrationTests.Support;

public sealed class ApiClient(HttpClient http)
{
    public const string CookieName = "taskly_token";

    private string? token;

    public bool HasCookie => !string.IsNullOrEmpty(token);

    public static string Json(params (string Key, object? Value)[] pairs) =>
        JsonSerializer.Serialize(pairs.ToDictionary(pair => pair.Key, pair => pair.Value));

    public Task<ApiResponse> GetAsync(string path, IDictionary<string, string>? headers = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), headers);

    public Task<ApiResponse> DeleteAsync(string path) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Delete, path), null);

    public Task<ApiResponse> OptionsAsync(string path, IDictionary<string, string> headers) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Options, path), headers);

    public Task<ApiResponse> PostJsonAsync(string path, string json) => SendJsonAsync(HttpMethod.Post, path, json);

    public Task<ApiResponse> PatchJsonAsync(string path, string json) => SendJsonAsync(HttpMethod.Patch, path, json);

    public Task<ApiResponse> PostRawAsync(string path, string body, string contentType)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };
        return SendAsync(request, null);
    }

    public Task<ApiResponse> UploadAsync(string path, string field, string filename, string contentType, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, field, filename } };
        return SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = form }, null);
    }

    public Task<ApiResponse> UploadEmptyFormAsync(string path) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new MultipartFormDataContent { { new StringContent("value"), "note" } } }, null);

    public void SetRawCookie(string value) => token = value;

    private Task<ApiResponse> SendJsonAsync(HttpMethod method, string path, string json)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return SendAsync(request, null);
    }

    private async Task<ApiResponse> SendAsync(HttpRequestMessage request, IDictionary<string, string>? headers)
    {
        if (headers is not null)
        {
            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (token is not null && !request.Headers.Contains("Cookie"))
        {
            request.Headers.TryAddWithoutValidation("Cookie", $"{CookieName}={token}");
        }

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            collected[header.Key] = string.Join(", ", header.Value);
        }

        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : new List<string>();
        TrackCookie(setCookies);

        return new ApiResponse((int)response.StatusCode, body, collected, setCookies);
    }

    private void TrackCookie(IEnumerable<string> setCookies)
    {
        foreach (var cookie in setCookies)
        {
            if (!cookie.StartsWith(CookieName + "=", StringComparison.Ordinal))
            {
                continue;
            }

            var end = cookie.IndexOf(';');
            var value = cookie[(CookieName.Length + 1)..(end < 0 ? cookie.Length : end)];
            var lower = cookie.ToLowerInvariant();
            var cleared = value.Length == 0 || lower.Contains("max-age=0") || lower.Contains("expires=thu, 01 jan 1970");
            token = cleared ? null : value;
        }
    }
}
