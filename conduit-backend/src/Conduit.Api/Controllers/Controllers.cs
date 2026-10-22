using Conduit.Api.Infrastructure;
using Conduit.Application.Articles;
using Conduit.Application.Comments;
using Conduit.Application.Identity;
using Conduit.Application.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conduit.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected int Me => int.Parse(User.FindFirst("sub")!.Value);
    protected int MeOrAnonymous => int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;
    protected Task<System.Text.Json.JsonElement> Body(string key, CancellationToken ct) => BodyReader.ReadAsync(Request, key, ct);
}

[Route("api/v1")]
public sealed class IdentityController(IdentityService identity) : ApiControllerBase
{
    [HttpPost("users/login")]
    public async Task<IActionResult> Login(CancellationToken ct) =>
        Ok(new { user = await identity.LoginAsync(await Body("user", ct), ct) });

    [HttpPost("users")]
    public async Task<IActionResult> Register(CancellationToken ct) =>
        StatusCode(201, new { user = await identity.RegisterAsync(await Body("user", ct), ct) });

    [Authorize, HttpGet("user")]
    public async Task<IActionResult> Current(CancellationToken ct) =>
        Ok(new { user = await identity.GetCurrentAsync(Me, ct) });

    [Authorize, HttpPut("user")]
    public async Task<IActionResult> Update(CancellationToken ct) =>
        Ok(new { user = await identity.UpdateAsync(Me, await Body("user", ct), ct) });
}

[Route("api/v1/profiles/{username}")]
public sealed class ProfilesController(ProfileService profiles) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string username, CancellationToken ct) =>
        Ok(new { profile = await profiles.GetAsync(username, MeOrAnonymous, ct) });

    [Authorize, HttpPost("follow")]
    public async Task<IActionResult> Follow(string username, CancellationToken ct) =>
        Ok(new { profile = await profiles.FollowAsync(username, Me, ct) });

    [Authorize, HttpDelete("follow")]
    public async Task<IActionResult> Unfollow(string username, CancellationToken ct) =>
        Ok(new { profile = await profiles.UnfollowAsync(username, Me, ct) });
}

[Route("api/v1/articles")]
public sealed class ArticlesController(ArticleService articles) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? tag, [FromQuery] string? author, [FromQuery] string? favorited,
                                          [FromQuery] string? offset, [FromQuery] string? limit, CancellationToken ct) =>
        Ok(await articles.ListAsync(tag, author, favorited, offset, limit, MeOrAnonymous, ct));

    [Authorize, HttpPost]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        StatusCode(201, new { article = await articles.CreateAsync(Me, await Body("article", ct), ct) });

    // Literal segment: wins over "{slug}" in attribute routing.
    [Authorize, HttpGet("feed")]
    public async Task<IActionResult> Feed([FromQuery] string? offset, [FromQuery] string? limit, CancellationToken ct) =>
        Ok(await articles.FeedAsync(offset, limit, Me, ct));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) =>
        Ok(new { article = await articles.GetAsync(slug, MeOrAnonymous, ct) });

    [Authorize, HttpPut("{slug}")]
    public async Task<IActionResult> Update(string slug, CancellationToken ct) =>
        Ok(new { article = await articles.UpdateAsync(slug, Me, await Body("article", ct), ct) });

    [Authorize, HttpDelete("{slug}")]
    public async Task<IActionResult> Delete(string slug, CancellationToken ct)
    {
        await articles.DeleteAsync(slug, Me, ct);
        return NoContent();
    }

    [Authorize, HttpPost("{slug}/favorite")]
    public async Task<IActionResult> Favorite(string slug, CancellationToken ct) =>
        Ok(new { article = await articles.FavoriteAsync(slug, Me, ct) });

    [Authorize, HttpDelete("{slug}/favorite")]
    public async Task<IActionResult> Unfavorite(string slug, CancellationToken ct) =>
        Ok(new { article = await articles.UnfavoriteAsync(slug, Me, ct) });
}

[Route("api/v1/articles/{slug}/comments")]
public sealed class CommentsController(CommentService comments) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string slug, CancellationToken ct) =>
        Ok(new { comments = await comments.ListAsync(slug, MeOrAnonymous, ct) });

    [Authorize, HttpPost]
    public async Task<IActionResult> Create(string slug, CancellationToken ct) =>
        StatusCode(201, new { comment = await comments.CreateAsync(slug, Me, await Body("comment", ct), ct) });

    [Authorize, HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string slug, string id, CancellationToken ct)
    {
        await comments.DeleteAsync(slug, id, Me, ct);
        return NoContent();
    }
}

[Route("api/v1/tags")]
public sealed class TagsController(ArticleService articles) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(new { tags = await articles.GetTagsAsync(ct) });
}
