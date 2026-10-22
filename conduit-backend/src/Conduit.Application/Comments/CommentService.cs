using System.Globalization;
using System.Text.Json;
using Conduit.Application.Abstractions;
using Conduit.Application.Common;
using Conduit.Application.Profiles;
using Conduit.Domain.Comments;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Application.Comments;

public sealed record CommentDto(int Id, DateTime CreatedAt, DateTime UpdatedAt, string Body, ProfileDto Author);
public sealed record CommentInput(Optional<string?> Body);

public sealed class CommentValidator : AbstractValidator<CommentInput>
{
    public static readonly CommentValidator Instance = new();
    public CommentValidator() =>
        RuleFor(x => x.Body.Value).Cascade(CascadeMode.Stop).Required().MaxChars(1000).Named("body");
}

public sealed class CommentService(IAppDbContext db, IClock clock)
{
    private async Task<int> ArticleIdAsync(string slug, CancellationToken ct) =>
        await db.Articles.AsNoTracking().Where(a => a.Slug == slug).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "article", "not found");

    public async Task<IReadOnlyList<CommentDto>> ListAsync(string slug, int me, CancellationToken ct)
    {
        var articleId = await ArticleIdAsync(slug, ct);
        return await db.Comments.AsNoTracking().Where(c => c.ArticleId == articleId)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Select(c => new CommentDto(c.Id, c.CreatedAt, c.UpdatedAt, c.Body,
                new ProfileDto(c.Author.Username, c.Author.Bio, c.Author.Image,
                               c.Author.Followers.Any(f => f.FollowerId == me))))
            .ToListAsync(ct);
    }

    public async Task<CommentDto> CreateAsync(string slug, int me, JsonElement body, CancellationToken ct)
    {
        var articleId = await ArticleIdAsync(slug, ct);
        var author = await db.Users.AsNoTracking().Where(u => u.Id == me)
            .Select(u => new ProfileDto(u.Username, u.Bio, u.Image, false)).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(401, "token", "is invalid");

        var r = new FieldReader(body);
        var input = new CommentInput(r.String("body"));
        Validate.Run(CommentValidator.Instance, input, r);

        var now = clock.UtcNow;
        var comment = new Comment { ArticleId = articleId, AuthorId = me, Body = input.Body.Value!, CreatedAt = now, UpdatedAt = now };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        return new CommentDto(comment.Id, now, now, comment.Body, author);
    }

    public async Task DeleteAsync(string slug, string rawId, int me, CancellationToken ct)
    {
        var articleId = await ArticleIdAsync(slug, ct);
        if (!int.TryParse(rawId, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new ApiException(422, "id", "is invalid");
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == id && c.ArticleId == articleId, ct)
                      ?? throw new ApiException(404, "comment", "not found");
        if (comment.AuthorId != me) throw new ApiException(403, "comment", "forbidden");
        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
    }
}
