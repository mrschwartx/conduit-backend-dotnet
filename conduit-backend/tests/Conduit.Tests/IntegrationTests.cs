using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Conduit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace Conduit.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        Environment.SetEnvironmentVariable("JWT_SECRET", new string('x', 40));
        Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", _db.GetConnectionString());
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b) => b.UseEnvironment("Testing");

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Api = "/api/v1";

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<(HttpClient client, string name)> NewUser()
    {
        var c = factory.CreateClient();
        var name = "u" + Guid.NewGuid().ToString("N")[..12];
        var r = await c.PostAsJsonAsync($"{Api}/users", new { user = new { username = name, email = $"{name}@example.com", password = "password123" } });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var user = (await Json(r)).GetProperty("user");
        Assert.Equal(JsonValueKind.Null, user.GetProperty("bio").ValueKind);   // null is written, not omitted
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", user.GetProperty("token").GetString());
        return (c, name);
    }

    private static Task<HttpResponseMessage> CreateArticle(HttpClient c, string title, string[]? tags = null) =>
        c.PostAsJsonAsync($"{Api}/articles", new { article = new { title, description = "d", body = "b", tagList = tags ?? ["t"] } });

    [Fact]
    public async Task Health_is_plain_OK_outside_the_prefix()
    {
        var r = await factory.CreateClient().GetAsync("/health");
        Assert.Equal("OK", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Feed_route_is_not_captured_by_slug_route()
    {
        var (c, _) = await NewUser();
        var r = await c.GetAsync($"{Api}/articles/feed");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.True((await Json(r)).TryGetProperty("articlesCount", out _));
    }

    [Fact]
    public async Task Missing_token_and_bearer_scheme_are_401_with_contract_body()
    {
        var anon = factory.CreateClient();
        var missing = await anon.GetAsync($"{Api}/user");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal("is missing", (await Json(missing)).GetProperty("errors").GetProperty("token")[0].GetString());

        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc.def.ghi");
        var optional = await anon.GetAsync($"{Api}/articles");   // optional-auth endpoint, bad token => 401
        Assert.Equal(HttpStatusCode.Unauthorized, optional.StatusCode);
    }

    [Fact]
    public async Task Bad_content_type_is_415_and_bad_json_is_400()
    {
        var c = factory.CreateClient();
        var wrongType = await c.PostAsync($"{Api}/users", new StringContent("{}", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        var badJson = await c.PostAsync($"{Api}/users", new StringContent("{nope", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, badJson.StatusCode);
    }

    [Fact]
    public async Task Register_validation_returns_422_for_all_fields_and_duplicates_409()
    {
        var c = factory.CreateClient();
        var bad = await c.PostAsJsonAsync($"{Api}/users", new { user = new { username = "a", email = "x", password = "1" } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        var errors = (await Json(bad)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("username", out _) && errors.TryGetProperty("email", out _) && errors.TryGetProperty("password", out _));

        var (_, name) = await NewUser();
        var dup = await c.PostAsJsonAsync($"{Api}/users", new { user = new { username = name.ToUpperInvariant(), email = "other@example.com", password = "password123" } });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Article_lifecycle_slugs_favorites_and_delete()
    {
        var (c, _) = await NewUser();
        var title = "Hello " + Guid.NewGuid().ToString("N")[..8];
        var first = (await Json(await CreateArticle(c, title))).GetProperty("article");
        var second = (await Json(await CreateArticle(c, title))).GetProperty("article");
        var slug = first.GetProperty("slug").GetString()!;
        Assert.Equal(slug + "-2", second.GetProperty("slug").GetString());

        var fav = await c.PostAsync($"{Api}/articles/{slug}/favorite", null);
        var favArticle = (await Json(fav)).GetProperty("article");
        Assert.Equal(1, favArticle.GetProperty("favoritesCount").GetInt32());
        Assert.True(favArticle.GetProperty("favorited").GetBoolean());

        var feedTitle = (await Json(await CreateArticle(c, "Feed"))).GetProperty("article").GetProperty("slug").GetString();
        Assert.StartsWith("feed-", feedTitle);

        var list = await Json(await c.GetAsync($"{Api}/articles?limit=1"));
        Assert.False(list.GetProperty("articles")[0].TryGetProperty("body", out _));   // list items omit body

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{Api}/articles/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Api}/articles/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Only_comment_author_can_delete_and_foreign_article_returns_403()
    {
        var (alice, _) = await NewUser();
        var (bob, _) = await NewUser();
        var slug = (await Json(await CreateArticle(alice, "C " + Guid.NewGuid().ToString("N")[..8])))
            .GetProperty("article").GetProperty("slug").GetString()!;

        var created = await alice.PostAsJsonAsync($"{Api}/articles/{slug}/comments", new { comment = new { body = "hi" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Json(created)).GetProperty("comment").GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync($"{Api}/articles/{slug}/comments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync($"{Api}/articles/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"{Api}/articles/{slug}/comments/{id}")).StatusCode);
    }

    [Fact]
    public async Task Following_yourself_is_422()
    {
        var (c, name) = await NewUser();
        var r = await c.PostAsync($"{Api}/profiles/{name}/follow", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }
}
