using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

internal static class OwnAccount
{
    public static void AssertOwned(Guid requesterId, string? userId, string message)
    {
        if (userId is null || !string.Equals(requesterId.ToString(), userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthenticationFailedException(message);
        }
    }
}
