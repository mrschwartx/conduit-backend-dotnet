namespace Taskly.Modules.Users.Domain;

public sealed class AvatarImageType
{
    public static readonly AvatarImageType Jpeg = new("image/jpeg", ".jpg");

    public static readonly AvatarImageType Png = new("image/png", ".png");

    public static readonly AvatarImageType Webp = new("image/webp", ".webp");

    public static readonly AvatarImageType Gif = new("image/gif", ".gif");

    private static readonly AvatarImageType[] All = [Jpeg, Png, Webp, Gif];

    private AvatarImageType(string contentType, string extension)
    {
        ContentType = contentType;
        Extension = extension;
    }

    public string ContentType { get; }

    public string Extension { get; }

    public static AvatarImageType? FromContentType(string? contentType)
    {
        if (contentType is null)
        {
            return null;
        }

        var normalized = contentType.ToLowerInvariant();
        return All.FirstOrDefault(type => type.ContentType == normalized);
    }
}
