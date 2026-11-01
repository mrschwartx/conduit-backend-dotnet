using Taskly.Modules.Users.Application;
using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;

namespace Taskly.UnitTests.Support;

public sealed class InMemoryUserRepository : IUserRepository
{
    public Dictionary<Guid, User> Users { get; } = new();

    public Guid NextId() => EntityIds.Generate();

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (!Users.ContainsKey(user.Id))
        {
            return Task.FromResult(false);
        }

        Users[user.Id] = user;
        return Task.FromResult(true);
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.GetValueOrDefault(id));

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.FirstOrDefault(user =>
            string.Equals(user.Email.Value, email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByEmailOrUsernameAsync(
        string? email,
        string? username,
        Guid? excludeId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Any(user =>
            user.Id != excludeId
            && ((email is not null && string.Equals(user.Email.Value, email, StringComparison.OrdinalIgnoreCase))
                || (username is not null && string.Equals(user.Username.Value, username, StringComparison.OrdinalIgnoreCase)))));

    public Task<IReadOnlyList<User>> SearchAsync(
        string? username,
        string? email,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<User> found = Users.Values
            .Where(user => string.IsNullOrEmpty(username)
                           || user.Username.Value.Contains(username, StringComparison.OrdinalIgnoreCase))
            .Where(user => string.IsNullOrEmpty(email)
                           || user.Email.Value.Contains(email, StringComparison.OrdinalIgnoreCase))
            .OrderBy(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .ToList();
        return Task.FromResult(found);
    }

    public Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Remove(id));
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string plain) => "hashed:" + plain;

    public bool Matches(string plain, string hash) => hash == "hashed:" + plain;
}

public sealed class FakeTokenService : ITokenService
{
    public string Issue(Guid userId) => "token:" + userId;

    public Task<Guid> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        if (token.StartsWith("token:", StringComparison.Ordinal) && Guid.TryParse(token[6..], out var id))
        {
            return Task.FromResult(id);
        }

        throw new InvalidTokenException("Invalid token");
    }
}

public sealed class RecordingAvatarStorage : IAvatarStorage
{
    private int counter;

    public Dictionary<string, byte[]> Stored { get; } = new();

    public List<string> Removed { get; } = [];

    public Task<string> StoreAsync(AvatarImageType type, byte[] content, CancellationToken cancellationToken = default)
    {
        counter++;
        var path = $"/images/users/avatar-{counter}-1{type.Extension}";
        Stored[path] = content;
        return Task.FromResult(path);
    }

    public Task RemoveAsync(string? avatarPath, CancellationToken cancellationToken = default)
    {
        if (AvatarPaths.IsCustom(avatarPath))
        {
            Removed.Add(avatarPath!);
            Stored.Remove(avatarPath!);
        }

        return Task.CompletedTask;
    }
}

public sealed class RecordingEventBus : IEventBus
{
    public List<IIntegrationEvent> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

public sealed class UsersFixture
{
    public InMemoryUserRepository Users { get; } = new();

    public FakePasswordHasher Hasher { get; } = new();

    public FakeTokenService Tokens { get; } = new();

    public RecordingAvatarStorage Avatars { get; } = new();

    public RecordingEventBus Events { get; } = new();

    public FixedTimeProvider Clock { get; } = new(Ids.FixedNow);

    public RegisterUser Register => new(Users, Hasher, Tokens, Clock);

    public AuthenticateUser Authenticate => new(Users, Hasher, Tokens);

    public GetUser Get => new(Users);

    public ListUsers List => new(Users);

    public UpdateUser Update => new(Users, Hasher, Clock);

    public ChangeUserAvatar ChangeAvatar => new(Users, Avatars, Clock);

    public DeleteUser Delete => new(Users, Avatars, Events, Clock);

    public VerifyAccessToken Verify => new(Tokens);

    public async Task<AuthResult> RegisterAsync(
        string username = "johndoe123",
        string email = "johndoe123@mail.com",
        string password = "password123") =>
        await Register.ExecuteAsync(Input.Of(("username", username), ("email", email), ("password", password)));
}
