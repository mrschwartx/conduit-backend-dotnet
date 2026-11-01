using Taskly.Host.Configuration;
using Taskly.Modules.Tasks.Application;
using Taskly.Modules.Users.Application;
using Taskly.SharedKernel.Domain;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.Host.Seeding;

public sealed class SeedCommand(
    AppSettings settings,
    SqlMigrator migrator,
    RegisterUser registerUser,
    CreateTask createTask,
    TimeProvider clock,
    ILogger<SeedCommand> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (settings.IsProduction && !settings.SeedAllowProduction)
        {
            logger.LogError("Refusing to seed a production database");
            return 1;
        }

        try
        {
            await migrator.MigrateAsync(cancellationToken);
            foreach (var entry in Entries())
            {
                await SeedEntryAsync(entry, cancellationToken);
            }

            logger.LogInformation("Seeding completed");
            return 0;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Seeding failed: {Message}", error.Message);
            return 1;
        }
    }

    private async Task SeedEntryAsync(SeedEntry entry, CancellationToken cancellationToken)
    {
        var username = entry.User["username"];
        try
        {
            var result = await registerUser.ExecuteAsync(entry.User, cancellationToken);
            var ownerId = Guid.Parse(result.User.Id);
            foreach (var task in entry.Tasks)
            {
                await createTask.ExecuteAsync(ownerId, task, cancellationToken);
            }

            logger.LogInformation("Seeded user {Username}", username);
        }
        catch (AlreadyExistsException)
        {
            logger.LogWarning("Skipped existing user {Username}", username);
        }
    }

    private List<SeedEntry> Entries()
    {
        var now = Timestamps.Format(Timestamps.Now(clock));
        return
        [
            new SeedEntry(
                new Dictionary<string, object?>
                {
                    ["username"] = "johndoe123",
                    ["email"] = "johndoe123@mail.com",
                    ["password"] = "password123"
                },
                [
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Read Atomic Habits",
                        ["description"] = "Finish reading Atomic Habits by James Clear",
                        ["priority"] = "low",
                        ["due"] = now,
                        ["status"] = "todo"
                    }
                ]),
            new SeedEntry(
                new Dictionary<string, object?>
                {
                    ["username"] = "jane78",
                    ["email"] = "jane@mail.com",
                    ["password"] = "securepassword456"
                },
                [
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Learn ASP.NET Core",
                        ["description"] = "Learn ASP.NET Core and build a full-stack application with it",
                        ["priority"] = "high",
                        ["due"] = now,
                        ["status"] = "todo"
                    }
                ])
        ];
    }

    private sealed record SeedEntry(
        IReadOnlyDictionary<string, object?> User,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Tasks);
}
