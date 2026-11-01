using System.Text.RegularExpressions;
using Taskly.IntegrationTests.Support;

namespace Taskly.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFixture api)
{
    private static string Credentials(string username, string email, string password) =>
        ApiClient.Json(("username", username), ("email", email), ("password", password));

    [Fact]
    public async Task SignUpCreatesAUserAndSetsTheAuthCookie()
    {
        var client = api.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..10];

        var response = await client.PostJsonAsync("/api/v1/auth/signup", Credentials("fresh_" + suffix, $"fresh_{suffix}@mail.com", ApiFixture.Password));

        Assert.Equal(200, response.Status);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", response.Text("_id"));
        Assert.Equal("fresh_" + suffix, response.Text("username"));
        Assert.Equal("/images/users/default-avatar.jpg", response.Text("avatar"));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", response.Text("createdAt"));
        Assert.DoesNotContain("password", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.True(client.HasCookie);

        var cookie = Assert.Single(response.SetCookies);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=3600", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignUpRejectsDuplicateEmailsAndUsernames()
    {
        var existing = await api.RegisterAsync();
        var client = api.CreateClient();

        var sameEmail = await client.PostJsonAsync("/api/v1/auth/signup", Credentials("another_" + Guid.NewGuid().ToString("N")[..8], existing.Email.ToUpperInvariant(), ApiFixture.Password));
        var sameUsername = await client.PostJsonAsync("/api/v1/auth/signup", Credentials(existing.Username.ToUpperInvariant(), $"x{Guid.NewGuid():N}@mail.com", ApiFixture.Password));

        Assert.Equal(422, sameEmail.Status);
        Assert.Equal("Email or Username is already registered.", sameEmail.Text("message"));
        Assert.Equal(422, sameUsername.Status);
        Assert.False(client.HasCookie);
    }

    [Fact]
    public async Task SignUpValidatesTheInput()
    {
        var client = api.CreateClient();
        var payloads = new[]
        {
            "{}",
            Credentials("ab", "a@mail.com", ApiFixture.Password),
            Credentials("valid_name", "not-an-email", ApiFixture.Password),
            Credentials("valid_name", "valid@mail.com", "short"),
            ApiClient.Json(("username", 5), ("email", "valid@mail.com"), ("password", ApiFixture.Password))
        };

        foreach (var payload in payloads)
        {
            var response = await client.PostJsonAsync("/api/v1/auth/signup", payload);
            Assert.Equal(400, response.Status);
            Assert.False(string.IsNullOrEmpty(response.Text("message")));
        }
    }

    [Fact]
    public async Task SignInWithValidCredentialsSetsTheCookie()
    {
        var user = await api.RegisterAsync();
        var client = api.CreateClient();

        var response = await client.PostJsonAsync("/api/v1/auth/signin", ApiClient.Json(("email", $"  {user.Email.ToUpperInvariant()} "), ("password", user.Password)));

        Assert.Equal(200, response.Status);
        Assert.Equal(user.Id, response.Text("_id"));
        Assert.True(client.HasCookie);
    }

    [Fact]
    public async Task SignInReportsUnknownUsersAndWrongPasswords()
    {
        var user = await api.RegisterAsync();
        var client = api.CreateClient();

        var unknown = await client.PostJsonAsync("/api/v1/auth/signin", ApiClient.Json(("email", "ghost@mail.com"), ("password", "Password123!")));
        var wrong = await client.PostJsonAsync("/api/v1/auth/signin", ApiClient.Json(("email", user.Email), ("password", "WrongPassword1")));

        Assert.Equal(404, unknown.Status);
        Assert.Equal("User not found!", unknown.Text("message"));
        Assert.Equal(401, wrong.Status);
        Assert.Equal("Wrong password!", wrong.Text("message"));
        Assert.False(client.HasCookie);
    }

    [Fact]
    public async Task SignInRejectsOperatorInjection()
    {
        var client = api.CreateClient();

        var response = await client.PostRawAsync(
            "/api/v1/auth/signin",
            "{\"email\":{\"$ne\":null},\"password\":{\"$ne\":null}}",
            "application/json");

        Assert.Equal(400, response.Status);
        Assert.Equal("Email and password are required", response.Text("message"));
    }

    [Fact]
    public async Task SignOutClearsTheCookie()
    {
        var user = await api.RegisterAsync();
        Assert.True(user.Client.HasCookie);

        var response = await user.Client.GetAsync("/api/v1/auth/signout");

        Assert.Equal(200, response.Status);
        Assert.Equal("Sign out successful", response.Text("message"));
        Assert.False(user.Client.HasCookie);
        var afterwards = await user.Client.GetAsync("/api/v1/users");
        Assert.Equal(401, afterwards.Status);
    }

    [Fact]
    public async Task IssuedTokensGrantAccessToProtectedRoutes()
    {
        var user = await api.RegisterAsync();

        var response = await user.Client.GetAsync("/api/v1/users/" + user.Id);

        Assert.Equal(200, response.Status);
        Assert.Equal(user.Username, response.Text("username"));
        Assert.True(Regex.IsMatch(response.Text("updatedAt"), @"Z$"));
    }
}
