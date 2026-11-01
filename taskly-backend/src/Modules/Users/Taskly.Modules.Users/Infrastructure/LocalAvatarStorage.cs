using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Taskly.Modules.Users.Application;
using Taskly.Modules.Users.Domain;

namespace Taskly.Modules.Users.Infrastructure;

public sealed class LocalAvatarStorage : IAvatarStorage
{
    private const int RandomBound = 1_000_000_000;

    private readonly string publicDirectory;

    private readonly string avatarsDirectory;

    private readonly TimeProvider clock;

    private readonly ILogger<LocalAvatarStorage> logger;

    public LocalAvatarStorage(string publicDirectory, TimeProvider clock, ILogger<LocalAvatarStorage> logger)
    {
        this.publicDirectory = Path.GetFullPath(publicDirectory);
        avatarsDirectory = Path.Combine(this.publicDirectory, "images", "users");
        this.clock = clock;
        this.logger = logger;
    }

    public async Task<string> StoreAsync(
        AvatarImageType type,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var filename = $"avatar-{clock.GetUtcNow().ToUnixTimeMilliseconds()}-{RandomNumberGenerator.GetInt32(RandomBound)}{type.Extension}";
        Directory.CreateDirectory(avatarsDirectory);

        await using var stream = new FileStream(
            Path.Combine(avatarsDirectory, filename),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            true);
        await stream.WriteAsync(content, cancellationToken);

        return AvatarPaths.ToPath(filename);
    }

    public Task RemoveAsync(string? avatarPath, CancellationToken cancellationToken = default)
    {
        if (!AvatarPaths.IsCustom(avatarPath))
        {
            return Task.CompletedTask;
        }

        var target = Path.GetFullPath(Path.Combine(publicDirectory, avatarPath!.TrimStart('/')));
        if (!target.StartsWith(avatarsDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        try
        {
            File.Delete(target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Failed to remove avatar file {AvatarPath}: {Message}", avatarPath, error.Message);
        }

        return Task.CompletedTask;
    }
}
