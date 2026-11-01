namespace Taskly.Modules.Users.Application;

public sealed record AuthResult(UserDto User, string Token);
