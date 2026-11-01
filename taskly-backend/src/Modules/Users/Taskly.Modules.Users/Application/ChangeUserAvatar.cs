using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class ChangeUserAvatar(IUserRepository users, IAvatarStorage avatarStorage, TimeProvider clock)
{
    public async Task<UserDto> ExecuteAsync(
        Guid requesterId,
        string userId,
        AvatarUpload? upload,
        CancellationToken cancellationToken = default)
    {
        OwnAccount.AssertOwned(requesterId, userId, "You can only upload an avatar for your own account");
        var id = EntityIds.Parse(userId, "user");

        if (upload is null)
        {
            throw new ValidationException("No file uploaded");
        }

        var type = AvatarImageType.FromContentType(upload.ContentType)
                   ?? throw new ValidationException("Only JPEG, PNG, WEBP and GIF images are allowed");

        var user = await users.FindByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("User not found!");

        var storedPath = await avatarStorage.StoreAsync(type, upload.Content, cancellationToken);
        try
        {
            var previousAvatar = user.Avatar;
            user.ChangeAvatar(storedPath, Timestamps.Now(clock));

            if (!await users.UpdateAsync(user, cancellationToken))
            {
                throw new NotFoundException("User not found!");
            }

            await avatarStorage.RemoveAsync(previousAvatar, cancellationToken);
            return UserDto.From(user);
        }
        catch
        {
            await avatarStorage.RemoveAsync(storedPath, CancellationToken.None);
            throw;
        }
    }
}
