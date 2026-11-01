using Npgsql;
using NpgsqlTypes;
using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.Modules.Tasks.Infrastructure;

public sealed class PostgresTaskRepository(NpgsqlDataSource dataSource) : ITaskRepository
{
    private const string Columns = "id, owner_id, name, description, priority, due, status, created_at, updated_at";

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["name"] = "name",
        ["description"] = "description",
        ["priority"] = "priority",
        ["due"] = "due",
        ["status"] = "status",
        ["createdAt"] = "created_at",
        ["updatedAt"] = "updated_at"
    };

    public Guid NextId() => EntityIds.Generate();

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO tasks (id, owner_id, name, description, priority, due, status, created_at, updated_at) "
            + "VALUES (@id, @owner_id, @name, @description, @priority, @due, @status, @created_at, @updated_at)");
        Bind(command, task);
        command.AddParameter("owner_id", NpgsqlDbType.Uuid, task.OwnerId);
        command.AddParameter("created_at", NpgsqlDbType.TimestampTz, task.CreatedAt.ToUniversalTime());

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException error) when (PostgresErrors.IsForeignKeyViolation(error))
        {
            throw new NotFoundException("User not found!");
        }
    }

    public async Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE tasks SET name = @name, description = @description, priority = @priority, due = @due, "
            + "status = @status, updated_at = @updated_at WHERE id = @id");
        Bind(command, task);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<TaskItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"SELECT {Columns} FROM tasks WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Uuid, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<TaskPage> FindByOwnerAsync(
        Guid ownerId,
        string? status,
        string? orderBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string filter = "owner_id = @owner_id AND (@status IS NULL OR status = @status)";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        long total;
        await using (var count = new NpgsqlCommand($"SELECT count(*) FROM tasks WHERE {filter}", connection))
        {
            count.AddParameter("owner_id", NpgsqlDbType.Uuid, ownerId);
            count.AddParameter("status", NpgsqlDbType.Text, status);
            total = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken));
        }

        var tasks = new List<TaskItem>();
        await using (var select = new NpgsqlCommand(
                         $"SELECT {Columns} FROM tasks WHERE {filter} ORDER BY {OrderClause(orderBy)} LIMIT @limit OFFSET @offset",
                         connection))
        {
            select.AddParameter("owner_id", NpgsqlDbType.Uuid, ownerId);
            select.AddParameter("status", NpgsqlDbType.Text, status);
            select.AddParameter("limit", NpgsqlDbType.Integer, pageSize);
            select.AddParameter("offset", NpgsqlDbType.Bigint, (long)(page - 1) * pageSize);

            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tasks.Add(Map(reader));
            }
        }

        return new TaskPage(tasks, total);
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM tasks WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Uuid, id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<int> DeleteByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM tasks WHERE owner_id = @owner_id");
        command.AddParameter("owner_id", NpgsqlDbType.Uuid, ownerId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string OrderClause(string? orderBy)
    {
        var columns = new List<string>();
        if (orderBy is not null)
        {
            if (!SortColumns.TryGetValue(orderBy, out var column))
            {
                throw new ArgumentException($"Unsupported sort field: {orderBy}", nameof(orderBy));
            }

            columns.Add(column);
        }

        foreach (var tieBreaker in new[] { "created_at", "id" })
        {
            if (!columns.Contains(tieBreaker))
            {
                columns.Add(tieBreaker);
            }
        }

        return string.Join(", ", columns);
    }

    private static void Bind(NpgsqlCommand command, TaskItem task)
    {
        command.AddParameter("id", NpgsqlDbType.Uuid, task.Id);
        command.AddParameter("name", NpgsqlDbType.Text, task.Name.Value);
        command.AddParameter("description", NpgsqlDbType.Text, task.Description.Value);
        command.AddParameter("priority", NpgsqlDbType.Text, task.Priority.Value);
        command.AddParameter("due", NpgsqlDbType.TimestampTz, task.Due.Value.ToUniversalTime());
        command.AddParameter("status", NpgsqlDbType.Text, task.Status.Value);
        command.AddParameter("updated_at", NpgsqlDbType.TimestampTz, task.UpdatedAt.ToUniversalTime());
    }

    private static TaskItem Map(NpgsqlDataReader reader) => TaskItem.Restore(
        reader.GetGuid(0),
        reader.GetGuid(1),
        TaskName.Restore(reader.GetString(2)),
        TaskDescription.Restore(reader.GetString(3)),
        TaskPriority.Restore(reader.GetString(4)),
        DueDate.Restore(reader.GetFieldValue<DateTimeOffset>(5)),
        TaskItemStatus.Restore(reader.GetString(6)),
        reader.GetFieldValue<DateTimeOffset>(7),
        reader.GetFieldValue<DateTimeOffset>(8));
}
