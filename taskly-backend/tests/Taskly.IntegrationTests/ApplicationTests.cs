using Taskly.IntegrationTests.Support;

namespace Taskly.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApplicationTests(ApiFixture api)
{
    [Fact]
    public async Task ServesTheWelcomeMessage()
    {
        var response = await api.CreateClient().GetAsync("/");

        Assert.Equal(200, response.Status);
        Assert.Equal("Welcome to the Taskly API", response.Text("message"));
    }

    [Fact]
    public async Task ReportsHealthyWhenTheDatabaseIsReachable()
    {
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(200, response.Status);
        Assert.Equal("Healthy", response.Body);
    }

    [Fact]
    public async Task ReturnsJsonNotFoundForUnknownRoutes()
    {
        var client = api.CreateClient();

        var unknown = await client.GetAsync("/api/v1/unknown");
        var fileLike = await client.GetAsync("/missing.js");
        var wrongMethod = await client.GetAsync("/api/v1/auth/signup");

        foreach (var response in new[] { unknown, fileLike, wrongMethod })
        {
            Assert.Equal(404, response.Status);
            Assert.Equal("Not found", response.Text("message"));
        }
    }

    [Fact]
    public async Task RejectsMalformedJsonBodies()
    {
        var response = await api.CreateClient().PostRawAsync("/api/v1/auth/signup", "{broken", "application/json");

        Assert.Equal(400, response.Status);
        Assert.Equal("Malformed JSON body", response.Text("message"));
        Assert.Equal(400, response.Json.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task IgnoresBodiesThatAreNotJson()
    {
        var response = await api.CreateClient().PostRawAsync("/api/v1/auth/signup", "username=x", "text/plain");

        Assert.Equal(400, response.Status);
        Assert.Equal("Username is required", response.Text("message"));
    }

    [Fact]
    public async Task RejectsOversizedJsonBodies()
    {
        var body = ApiClient.Json(("username", new string('a', 200 * 1024)));

        var response = await api.CreateClient().PostRawAsync("/api/v1/auth/signup", body, "application/json");

        Assert.Equal(413, response.Status);
    }

    [Fact]
    public async Task AnswersCorsPreflightForTheConfiguredClient()
    {
        var response = await api.CreateClient().OptionsAsync(
            "/api/v1/auth/signin",
            new Dictionary<string, string>
            {
                ["Origin"] = ApiFixture.ClientOrigin,
                ["Access-Control-Request-Method"] = "POST",
                ["Access-Control-Request-Headers"] = "content-type"
            });

        Assert.Equal(204, response.Status);
        Assert.Equal(ApiFixture.ClientOrigin, response.Header("Access-Control-Allow-Origin"));
        Assert.Equal("true", response.Header("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task DoesNotGrantCorsToOtherOrigins()
    {
        var response = await api.CreateClient().GetAsync(
            "/",
            new Dictionary<string, string> { ["Origin"] = "http://evil.example" });

        Assert.Equal(200, response.Status);
        Assert.Null(response.Header("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ProtectsUserAndTaskRoutesWithoutAToken()
    {
        var client = api.CreateClient();
        var paths = new[]
        {
            "/api/v1/users",
            "/api/v1/users/" + Guid.NewGuid(),
            "/api/v1/tasks/" + Guid.NewGuid(),
            "/api/v1/tasks/user/" + Guid.NewGuid(),
            "/API/V1/USERS"
        };

        foreach (var path in paths)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(401, response.Status);
            Assert.Equal("Unauthorized: Token not provided", response.Text("message"));
        }
    }

    [Fact]
    public async Task RejectsInvalidTokens()
    {
        var client = api.CreateClient();
        client.SetRawCookie("garbage");

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(403, response.Status);
        Assert.Equal("Forbidden: Invalid token", response.Text("message"));
    }

    [Fact]
    public async Task ServesStaticFilesWithNosniff()
    {
        var name = $"static-{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(api.AvatarsDirectory, name), new byte[] { 137, 80, 78, 71 });
        var client = api.CreateClient();

        var found = await client.GetAsync($"/images/users/{name}");
        var missing = await client.GetAsync("/images/users/missing.png");

        Assert.Equal(200, found.Status);
        Assert.Equal("image/png", found.Header("Content-Type"));
        Assert.Equal("nosniff", found.Header("X-Content-Type-Options"));
        Assert.Equal(404, missing.Status);
    }
}
