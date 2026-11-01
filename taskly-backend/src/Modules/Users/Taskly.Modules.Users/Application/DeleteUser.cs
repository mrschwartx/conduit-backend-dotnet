using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class DeleteUser(
    IUserRepository users,
    IAvatarStorage avatarStorage,
    IEventBus eventBus,
    TimeProvider clock)
{
    public async Task ExecuteAsync(Guid requesterId, string userId, CancellationToken cancellationToken = default)
    {
        OwnAccount.AssertOwned(requesterId, userId, "You can only delete your own account");
        var id = EntityIds.Parse(userId, "user");

        var user = await users.FindByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("User not found!");

        if (!await users.DeleteByIdAsync(id, cancellationToken))
        {
            throw new NotFoundException("User not found!");
        }

        await avatarStorage.RemoveAsync(user.Avatar, cancellationToken);
        await eventBus.PublishAsync(new UserDeleted(id, Timestamps.Now(clock)), cancellationToken);
    }
}
