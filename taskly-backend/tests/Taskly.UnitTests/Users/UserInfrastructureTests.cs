using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Taskly.Modules.Users.Domain;
using Taskly.Modules.Users.Infrastructure;
using Taskly.UnitTests.Support;

namespace Taskly.UnitTests.Users;

public sealed class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher hasher = new(4);

    [Fact]
    public void HashesAndVerifiesPasswords()
    {
        var hash = hasher.Hash("password123");

        Assert.StartsWith("$2", hash);
        Assert.NotEqual("password123", hash);
        Assert.True(hasher.Matches("password123", hash));
        Assert.False(hasher.Matches("password124", hash));
    }

    [Fact]
    public void SaltsEveryHash()
    {
        Assert.NotEqual(hasher.Hash("password123"), hasher.Hash("password123"));
    }
}

public sealed class JwtTokenServiceTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private readonly FixedTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private JwtTokenService CreateService(string secret = Secret) => new(secret, TimeSpan.FromHours(1), clock);

    [Fact]
    public async Task IssuesTokensThatVerifyToTheSameUser()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var token = service.Issue(userId);

        Assert.Equal(userId, await service.VerifyAsync(token));
    }

    [Fact]
    public void EncodesTheIdAndAOneHourLifetime()
    {
        var userId = Guid.NewGuid();
        var token = CreateService().Issue(userId);

        var parts = token.Split('.');
        var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0])).RootElement;
        var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1])).RootElement;

        Assert.Equal("HS256", header.GetProperty("alg").GetString());
        Assert.Equal(userId.ToString(), payload.GetProperty("id").GetString());
        Assert.Equal(3600, payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64());
    }

    [Fact]
    public async Task RejectsExpiredTokens()
    {
        var service = CreateService();
        var token = service.Issue(Guid.NewGuid());

        clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<InvalidTokenException>(() => service.VerifyAsync(token));
    }

    [Fact]
    public async Task AcceptsTokensUntilTheyExpire()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();
        var token = service.Issue(userId);

        clock.Advance(TimeSpan.FromMinutes(59));

        Assert.Equal(userId, await service.VerifyAsync(token));
    }

    [Fact]
    public async Task RejectsTokensSignedWithAnotherSecret()
    {
        var token = CreateService("ffffffffffffffffffffffffffffffff").Issue(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidTokenException>(() => CreateService().VerifyAsync(token));
    }

    [Fact]
    public async Task RejectsTamperedAndMalformedTokens()
    {
        var service = CreateService();
        var token = service.Issue(Guid.NewGuid());
        var parts = token.Split('.');
        var flipped = (parts[2][0] == 'A' ? 'B' : 'A') + parts[2][1..];
        var tampered = $"{parts[0]}.{parts[1]}.{flipped}";

        foreach (var candidate in new[] { tampered, "garbage", "a.b.c", string.Empty })
        {
            await Assert.ThrowsAsync<InvalidTokenException>(() => service.VerifyAsync(candidate));
        }
    }

    [Fact]
    public async Task RejectsUnsignedTokens()
    {
        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var expires = clock.GetUtcNow().AddHours(1).ToUnixTimeSeconds();
        var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{{\"id\":\"{Guid.NewGuid()}\",\"exp\":{expires}}}"));

        await Assert.ThrowsAsync<InvalidTokenException>(() => CreateService().VerifyAsync($"{header}.{payload}."));
    }

    [Fact]
    public async Task RejectsSignedTokensWithoutAValidUserId()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var handler = new JsonWebTokenHandler();

        foreach (var claims in new[]
                 {
                     new Dictionary<string, object> { ["id"] = "not-a-uuid" },
                     new Dictionary<string, object> { ["other"] = "value" }
                 })
        {
            var token = handler.CreateToken(new SecurityTokenDescriptor
            {
                Claims = claims,
                Expires = clock.GetUtcNow().AddHours(1).UtcDateTime,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            });

            await Assert.ThrowsAsync<InvalidTokenException>(() => CreateService().VerifyAsync(token));
        }
    }

    [Fact]
    public async Task RejectsSignedTokensWithoutExpiration()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { ["id"] = Guid.NewGuid().ToString() },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });

        await Assert.ThrowsAsync<InvalidTokenException>(() => CreateService().VerifyAsync(token));
    }
}

public sealed class LocalAvatarStorageTests : IDisposable
{
    private readonly string publicDirectory = Path.Combine(Path.GetTempPath(), "taskly-avatars-" + Guid.NewGuid().ToString("N"));

    private readonly FixedTimeProvider clock = new(Ids.FixedNow);

    private LocalAvatarStorage CreateStorage() =>
        new(publicDirectory, clock, NullLogger<LocalAvatarStorage>.Instance);

    private string AvatarsDirectory => Path.Combine(publicDirectory, "images", "users");

    [Fact]
    public async Task StoresAvatarFilesUnderTheUsersImageDirectory()
    {
        var path = await CreateStorage().StoreAsync(AvatarImageType.Png, [1, 2, 3]);

        Assert.True(AvatarPaths.IsCustom(path));
        Assert.EndsWith(".png", path);
        var file = Path.Combine(publicDirectory, path.TrimStart('/'));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(file));
    }

    [Fact]
    public async Task StoresEveryUploadUnderAUniqueName()
    {
        var storage = CreateStorage();

        var first = await storage.StoreAsync(AvatarImageType.Png, [1]);
        var second = await storage.StoreAsync(AvatarImageType.Png, [2]);

        Assert.NotEqual(first, second);
        Assert.Equal(2, Directory.GetFiles(AvatarsDirectory).Length);
    }

    [Fact]
    public async Task RemovesStoredAvatars()
    {
        var storage = CreateStorage();
        var path = await storage.StoreAsync(AvatarImageType.Gif, [1]);

        await storage.RemoveAsync(path);

        Assert.Empty(Directory.GetFiles(AvatarsDirectory));
    }

    [Fact]
    public async Task IgnoresDefaultMissingAndSuspiciousPaths()
    {
        var storage = CreateStorage();
        Directory.CreateDirectory(AvatarsDirectory);
        var keeper = Path.Combine(AvatarsDirectory, "default-avatar.jpg");
        await File.WriteAllBytesAsync(keeper, new byte[] { 1 });

        await storage.RemoveAsync(AvatarPaths.DefaultPath);
        await storage.RemoveAsync("/images/users/avatar-missing.png");
        await storage.RemoveAsync("/images/users/avatar-../../etc/passwd");
        await storage.RemoveAsync(null);

        Assert.True(File.Exists(keeper));
    }

    public void Dispose()
    {
        if (Directory.Exists(publicDirectory))
        {
            Directory.Delete(publicDirectory, true);
        }
    }
}
