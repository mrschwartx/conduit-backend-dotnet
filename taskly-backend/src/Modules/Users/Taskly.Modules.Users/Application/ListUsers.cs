using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class ListUsers(IUserRepository users)
{
    public async Task<IReadOnlyList<UserDto>> ExecuteAsync(
        object? username,
        object? email,
        CancellationToken cancellationToken = default)
    {
        var usernameFilter = OptionalString(username, "Username");
        var emailFilter = OptionalString(email, "Email");

        var found = await users.SearchAsync(usernameFilter, emailFilter, cancellationToken);
        if (found.Count == 0)
        {
            throw new NotFoundException("No users found");
        }

        return found.Select(UserDto.From).ToList();
    }

    private static string? OptionalString(object? value, string label)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not string text)
        {
            throw new ValidationException($"{label} filter must be a string");
        }

        return text;
    }
}
