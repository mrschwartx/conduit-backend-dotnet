namespace Taskly.Modules.Users.Application;

public sealed record AvatarUpload(string? ContentType, byte[] Content);
