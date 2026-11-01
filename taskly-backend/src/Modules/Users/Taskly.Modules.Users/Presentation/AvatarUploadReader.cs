using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Taskly.Modules.Users.Application;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Presentation;

public static class AvatarUploadReader
{
    public const string FieldName = "avatar";

    public const long MultipartOverheadBytes = 64 * 1024;

    private const long BytesPerMegabyte = 1024 * 1024;

    public static async Task<AvatarUpload?> ReadAsync(
        HttpRequest request,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (request.ContentType is null
            || !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sizeMessage = $"File size exceeds {maxBytes / (double)BytesPerMegabyte:0.##}MB";
        var limit = maxBytes + MultipartOverheadBytes;

        var sizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = limit;
        }

        if (request.ContentLength is { } declared && declared > limit)
        {
            throw new ValidationException(sizeMessage);
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException)
        {
            throw new ValidationException(sizeMessage);
        }
        catch (InvalidDataException)
        {
            throw new ValidationException("Malformed multipart body");
        }

        var file = form.Files.GetFile(FieldName);
        if (file is null)
        {
            if (form.Files.Count > 0)
            {
                throw new ValidationException("Unexpected field");
            }

            return null;
        }

        if (file.Length > maxBytes)
        {
            throw new ValidationException(sizeMessage);
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return new AvatarUpload(file.ContentType, buffer.ToArray());
    }
}
