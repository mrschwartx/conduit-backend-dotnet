namespace Taskly.Modules.Users.Domain;

public sealed class User
{
    private User(
        Guid id,
        Username username,
        Email email,
        string passwordHash,
        string avatar,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Avatar = avatar;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public Username Username { get; private set; }

    public Email Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string Avatar { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Register(Guid id, Username username, Email email, string passwordHash, DateTimeOffset now) =>
        new(id, username, email, passwordHash, AvatarPaths.DefaultPath, now, now);

    public static User Restore(
        Guid id,
        Username username,
        Email email,
        string passwordHash,
        string avatar,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id, username, email, passwordHash, avatar, createdAt, updatedAt);

    public void UpdateProfile(Username? username, Email? email, string? passwordHash, DateTimeOffset now)
    {
        if (username is not null)
        {
            Username = username;
        }

        if (email is not null)
        {
            Email = email;
        }

        if (passwordHash is not null)
        {
            PasswordHash = passwordHash;
        }

        UpdatedAt = now;
    }

    public void ChangeAvatar(string avatar, DateTimeOffset now)
    {
        Avatar = avatar;
        UpdatedAt = now;
    }
}
