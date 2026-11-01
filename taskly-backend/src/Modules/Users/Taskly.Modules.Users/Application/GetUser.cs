using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class GetUser(IUserRepository users)
{
    public async Task<UserDto> ExecuteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var id = EntityIds.Parse(userId, "user");
        var user = await users.FindByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("User not found!");
        return UserDto.From(user);
    }
}
