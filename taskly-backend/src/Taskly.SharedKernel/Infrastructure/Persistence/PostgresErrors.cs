using Npgsql;

namespace Taskly.SharedKernel.Infrastructure.Persistence;

public static class PostgresErrors
{
    public static bool IsUniqueViolation(Exception error) =>
        error is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static bool IsForeignKeyViolation(Exception error) =>
        error is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };
}
