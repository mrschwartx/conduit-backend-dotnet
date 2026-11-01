using Npgsql;
using NpgsqlTypes;
using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.Modules.Users.Infrastructure;

public sealed class PostgresUserRepository(NpgsqlDataSource dataSource) : IUserRepository
{
    private const string DuplicateMessage = "Email or Username is already registered.";

    private const string Columns = "id, username, email, password_hash, avatar_file, created_at, updated_at";

    public Guid NextId() => EntityIds.Generate();

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO users (id, username, email, password_hash, avatar_file, created_at, updated_at) "
            + "VALUES (@id, @username, @email, @password_hash, @avatar_file, @created_at, @updated_at)");
        Bind(command, user);
        command.AddParameter("created_at", NpgsqlDbType.TimestampTz, user.CreatedAt.ToUniversalTime());

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException error) when (PostgresErrors.IsUniqueViolation(error))
        {
            throw new AlreadyExistsException(DuplicateMessage);
        }
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE users SET username = @username, email = @email, password_hash = @password_hash, "
            + "avatar_file = @avatar_file, updated_at = @updated_at WHERE id = @id");
        Bind(command, user);

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (PostgresException error) when (PostgresErrors.IsUniqueViolation(error))
        {
            throw new AlreadyExistsException(DuplicateMessage);
        }
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"SELECT {Columns} FROM users WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Uuid, id);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT {Columns} FROM users WHERE lower(email) = lower(@email)");
        command.AddParameter("email", NpgsqlDbType.Text, email);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<bool> ExistsByEmailOrUsernameAsync(
        string? email,
        string? username,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM users "
            + "WHERE (lower(email) = lower(@email) OR lower(username) = lower(@username)) "
            + "AND (@exclude_id IS NULL OR id <> @exclude_id))");
        command.AddParameter("email", NpgsqlDbType.Text, email);
        command.AddParameter("username", NpgsqlDbType.Text, username);
        command.AddParameter("exclude_id", NpgsqlDbType.Uuid, excludeId);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlyList<User>> SearchAsync(
        string? username,
        string? email,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT {Columns} FROM users "
            + "WHERE (@username IS NULL OR username ILIKE @username) "
            + "AND (@email IS NULL OR email ILIKE @email) "
            + "ORDER BY created_at ASC, id ASC");
        command.AddParameter("username", NpgsqlDbType.Text, LikePattern(username));
        command.AddParameter("email", NpgsqlDbType.Text, LikePattern(email));

        var users = new List<User>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(Map(reader));
        }

        return users;
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM users WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Uuid, id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void Bind(NpgsqlCommand command, User user)
    {
        command.AddParameter("id", NpgsqlDbType.Uuid, user.Id);
        command.AddParameter("username", NpgsqlDbType.Text, user.Username.Value);
        command.AddParameter("email", NpgsqlDbType.Text, user.Email.Value);
        command.AddParameter("password_hash", NpgsqlDbType.Text, user.PasswordHash);
        command.AddParameter("avatar_file", NpgsqlDbType.Text, user.Avatar);
        command.AddParameter("updated_at", NpgsqlDbType.TimestampTz, user.UpdatedAt.ToUniversalTime());
    }

    private static async Task<User?> ReadSingleAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static User Map(NpgsqlDataReader reader) => User.Restore(
        reader.GetGuid(0),
        Username.Restore(reader.GetString(1)),
        Email.Restore(reader.GetString(2)),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetFieldValue<DateTimeOffset>(5),
        reader.GetFieldValue<DateTimeOffset>(6));

    private static string? LikePattern(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var escaped = value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
