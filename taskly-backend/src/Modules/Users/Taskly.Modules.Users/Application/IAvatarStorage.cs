using Taskly.Modules.Users.Domain;

namespace Taskly.Modules.Users.Application;

public interface IAvatarStorage
{
    Task<string> StoreAsync(AvatarImageType type, byte[] content, CancellationToken cancellationToken = default);

    Task RemoveAsync(string? avatarPath, CancellationToken cancellationToken = default);
}
