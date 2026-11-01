using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Taskly.SharedKernel.Domain;

namespace Taskly.SharedKernel.Infrastructure.Http;

public static class RequestBodyReader
{
    public const int MaxBytes = 100 * 1024;

    private const string TooLargeMessage = "Request body is too large";

    private const string MalformedMessage = "Malformed JSON body";

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    public static async Task<IReadOnlyDictionary<string, object?>> ReadJsonAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsJson(request.ContentType))
        {
            return Empty;
        }

        if (request.ContentLength is > MaxBytes)
        {
            throw new PayloadTooLargeException(TooLargeMessage);
        }

        var bytes = await ReadBytesAsync(request.Body, cancellationToken);
        if (IsBlank(bytes))
        {
            return Empty;
        }

        return Parse(bytes);
    }

    public static IReadOnlyDictionary<string, object?> Parse(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ValidationException(MalformedMessage);
            }

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = Convert(property.Value);
            }

            return values;
        }
        catch (JsonException)
        {
            throw new ValidationException(MalformedMessage);
        }
    }

    public static bool IsJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var separator = contentType.IndexOf(';');
        var mediaType = (separator < 0 ? contentType : contentType[..separator]).Trim().ToLowerInvariant();
        return mediaType == "application/json" || mediaType.EndsWith("+json", StringComparison.Ordinal);
    }

    private static async Task<byte[]> ReadBytesAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
            {
                throw new PayloadTooLargeException(TooLargeMessage);
            }
        }

        return buffer.ToArray();
    }

    private static bool IsBlank(byte[] bytes)
    {
        foreach (var value in bytes)
        {
            if (value is not (0x20 or 0x09 or 0x0A or 0x0D))
            {
                return false;
            }
        }

        return true;
    }

    private static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Null => null,
        _ => element.Clone()
    };
}
