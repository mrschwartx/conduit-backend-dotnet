using System.Text.RegularExpressions;

namespace Taskly.Modules.Users.Domain;

public static class AvatarPaths
{
    public const string UrlPrefix = "/images/users/";

    public const string DefaultPath = UrlPrefix + "default-avatar.jpg";

    private static readonly Regex CustomPattern = new(
        @"^/images/users/avatar-[A-Za-z0-9_.\-]+\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    public static bool IsCustom(string? avatarPath) => avatarPath is not null && CustomPattern.IsMatch(avatarPath);

    public static string ToPath(string filename) => UrlPrefix + filename;
}
