namespace Taskly.IntegrationTests.Support;

public sealed record RegisteredUser(ApiClient Client, string Id, string Username, string Email, string Password);

public sealed class ApiFixture : IAsyncLifetime
{
    public const string Password = "Password123!";

    public const string ClientOrigin = "http://localhost:3000";

    private TestDatabase? database;

    private TasklyApiFactory? factory;

    public string PublicDirectory { get; private set; } = string.Empty;

    public string AvatarsDirectory => Path.Combine(PublicDirectory, "images", "users");

    public async Task InitializeAsync()
    {
        database = await TestDatabase.CreateAsync();
        PublicDirectory = Path.Combine(Path.GetTempPath(), "taskly-public-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(AvatarsDirectory);

        Environment.SetEnvironmentVariable("DATABASE_URL", database.ConnectionString);
        Environment.SetEnvironmentVariable("AUTH_SECRET", "integration-test-secret-integration-test-secret");
        Environment.SetEnvironmentVariable("BCRYPT_ROUNDS", "4");
        Environment.SetEnvironmentVariable("PUBLIC_DIR", PublicDirectory);
        Environment.SetEnvironmentVariable("CLIENT_URL", ClientOrigin);
        Environment.SetEnvironmentVariable("APP_LEVEL", "development");
        Environment.SetEnvironmentVariable("LOG_LEVEL", "warning");

        factory = new TasklyApiFactory();
        factory.CreateClient().Dispose();
    }

    public async Task DisposeAsync()
    {
        factory?.Dispose();
        if (database is not null)
        {
            await database.DisposeAsync();
        }

        if (Directory.Exists(PublicDirectory))
        {
            Directory.Delete(PublicDirectory, true);
        }
    }

    public ApiClient CreateClient() => factory!.CreateApiClient();

    public async Task<RegisteredUser> RegisterAsync(string? prefix = null)
    {
        var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var username = $"{prefix ?? "user"}_{suffix}";
        var email = $"{username}@mail.com";

        var response = await client.PostJsonAsync(
            "/api/v1/auth/signup",
            ApiClient.Json(("username", username), ("email", email), ("password", Password)));
        Assert.Equal(200, response.Status);

        return new RegisteredUser(client, response.Text("_id"), username, email, Password);
    }

    public static string TaskJson(
        string name = "Read Atomic Habits",
        string priority = "low",
        string due = "2026-12-08T10:00:00Z",
        string? status = null,
        string? description = "Finish reading Atomic Habits by James Clear")
    {
        var fields = new List<(string, object?)>
        {
            ("name", name),
            ("priority", priority),
            ("due", due)
        };
        if (description is not null)
        {
            fields.Add(("description", description));
        }

        if (status is not null)
        {
            fields.Add(("status", status));
        }

        return ApiClient.Json(fields.ToArray());
    }
}

[CollectionDefinition(ApiCollection.Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
