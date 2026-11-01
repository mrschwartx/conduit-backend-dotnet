namespace Taskly.Modules.Users;

public sealed record UsersModuleOptions(
    string AuthSecret,
    TimeSpan TokenTtl,
    int BcryptRounds,
    string CookieName,
    string? CookieDomain,
    bool Production,
    string PublicDirectory,
    long AvatarMaxBytes);
