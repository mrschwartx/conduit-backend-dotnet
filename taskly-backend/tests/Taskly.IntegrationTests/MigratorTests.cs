using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Taskly.IntegrationTests.Support;
using Taskly.Modules.Tasks;
using Taskly.Modules.Users;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.IntegrationTests;

public sealed class MigratorTests : IAsyncLifetime
{
    private TestDatabase database = null!;

    private NpgsqlDataSource dataSource = null!;

    public async Task InitializeAsync()
    {
        database = await TestDatabase.CreateAsync();
        dataSource = NpgsqlDataSource.Create(database.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        await database.DisposeAsync();
    }

    private SqlMigrator CreateMigrator(params IMigrationSource[] sources) =>
        new(dataSource, sources, NullLogger<SqlMigrator>.Instance);

    [Fact]
    public async Task AppliesScriptsInNameOrderAndRecordsThem()
    {
        var source = new ListMigrationSource(
            ("0002_second.sql", "CREATE TABLE second_table (id INT);"),
            ("0001_first.sql", "CREATE TABLE first_table (id INT);"));

        var executed = await CreateMigrator(source).MigrateAsync();

        Assert.Equal(new[] { "0001_first.sql", "0002_second.sql" }, executed.ToArray());
        Assert.True(await database.TableExistsAsync(dataSource, "first_table"));
        Assert.True(await database.TableExistsAsync(dataSource, "second_table"));
        Assert.Equal(new[] { "0001_first.sql", "0002_second.sql" }, await RecordedAsync());
    }

    [Fact]
    public async Task IsIdempotent()
    {
        var migrator = CreateMigrator(new ListMigrationSource(("0001_first.sql", "CREATE TABLE first_table (id INT);")));

        await migrator.MigrateAsync();
        var second = await migrator.MigrateAsync();

        Assert.Empty(second);
        Assert.Single(await RecordedAsync());
    }

    [Fact]
    public async Task RollsBackAFailingScriptAndKeepsEarlierOnes()
    {
        var source = new ListMigrationSource(
            ("0001_ok.sql", "CREATE TABLE kept_table (id INT);"),
            ("0002_broken.sql", "CREATE TABLE rolled_back (id INT); SELECT * FROM missing_table;"));

        await Assert.ThrowsAsync<PostgresException>(() => CreateMigrator(source).MigrateAsync());

        Assert.True(await database.TableExistsAsync(dataSource, "kept_table"));
        Assert.False(await database.TableExistsAsync(dataSource, "rolled_back"));
        Assert.Equal(new[] { "0001_ok.sql" }, await RecordedAsync());
    }

    [Fact]
    public async Task RejectsDuplicateScriptNames()
    {
        var first = new ListMigrationSource(("0001_same.sql", "SELECT 1;"));
        var second = new ListMigrationSource(("0001_same.sql", "SELECT 2;"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateMigrator(first, second).MigrateAsync());
    }

    [Fact]
    public async Task AppliesTheModuleSchemasInDependencyOrder()
    {
        var migrator = CreateMigrator(
            new EmbeddedMigrationSource(typeof(TasksModule).Assembly),
            new EmbeddedMigrationSource(typeof(UsersModule).Assembly));

        var executed = await migrator.MigrateAsync();

        Assert.Equal(new[] { "0001_create_users.sql", "0002_create_tasks.sql" }, executed.ToArray());
        Assert.True(await database.TableExistsAsync(dataSource, "users"));
        Assert.True(await database.TableExistsAsync(dataSource, "tasks"));
    }

    private async Task<string[]> RecordedAsync()
    {
        var names = new List<string>();
        await using var command = dataSource.CreateCommand("SELECT name FROM schema_migrations ORDER BY name");
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }

    private sealed class ListMigrationSource(params (string Name, string Sql)[] scripts) : IMigrationSource
    {
        public IEnumerable<MigrationScript> Scripts() =>
            scripts.Select(script => new MigrationScript(script.Name, () => script.Sql));
    }
}
