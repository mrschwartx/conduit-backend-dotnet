using System.Linq.Expressions;
using System.Text.Json;
using Conduit.Application.Abstractions;
using Conduit.Application.Common;
using Conduit.Application.Profiles;
using Conduit.Domain.Articles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Application.Articles;

public sealed record ArticleDto(string Slug, string Title, string Description, string Body, IReadOnlyList<string> TagList,
    DateTime CreatedAt, DateTime UpdatedAt, bool Favorited, int FavoritesCount, ProfileDto Author);

public sealed record ArticleListItemDto(string Slug, string Title, string Description, IReadOnlyList<string> TagList,
    DateTime CreatedAt, DateTime UpdatedAt, bool Favorited, int FavoritesCount, ProfileDto Author);

public sealed record ArticleListDto(IReadOnlyList<ArticleListItemDto> Articles, int ArticlesCount);

public sealed record ArticleInput(Optional<string?> Title, Optional<string?> Description, Optional<string?> Body,
                                  Optional<List<string>?> TagList);

internal static class ArticleRules
{
    public static IRuleBuilderOptions<ArticleInput, List<string>?> Tags(IRuleBuilder<ArticleInput, List<string>?> r) => r
        .Must(l => l is null || l.Count <= 10).WithMessage("is too long (maximum is 10 tags)")
        .Must(l => l is null || l.All(t => t.Length > 0)).WithMessage("contains a blank tag")
        .Must(l => l is null || l.All(t => t.EnumerateRunes().Count() <= 30)).WithMessage("contains a tag longer than 30 characters")
        .Must(l => l is null || l.All(t => !t.Any(c => char.IsWhiteSpace(c) || c == ','))).WithMessage("contains an invalid tag (no spaces or commas)");
}

public sealed class NewArticleValidator : AbstractValidator<ArticleInput>
{
    public static readonly NewArticleValidator Instance = new();
    public NewArticleValidator()
    {
        RuleFor(x => x.Title.Value).Cascade(CascadeMode.Stop).Required().MaxChars(100).Named("title");
        RuleFor(x => x.Description.Value).Cascade(CascadeMode.Stop).Required().MaxChars(200).Named("description");
        RuleFor(x => x.Body.Value).Cascade(CascadeMode.Stop).Required().MaxChars(20000).Named("body");
        ArticleRules.Tags(RuleFor(x => x.TagList.Value)).OverridePropertyName("tagList");
    }
}

public sealed class UpdateArticleValidator : AbstractValidator<ArticleInput>
{
    public static readonly UpdateArticleValidator Instance = new();
    public UpdateArticleValidator()
    {
        RuleFor(x => x.Title.Value).Cascade(CascadeMode.Stop).Required().MaxChars(100).Named("title").When(x => x.Title.HasValue);
        RuleFor(x => x.Description.Value).Cascade(CascadeMode.Stop).Required().MaxChars(200).Named("description").When(x => x.Description.HasValue);
        RuleFor(x => x.Body.Value).Cascade(CascadeMode.Stop).Required().MaxChars(20000).Named("body").When(x => x.Body.HasValue);
        RuleFor(x => x.TagList.Value).NotNull().WithMessage("can't be blank").OverridePropertyName("tagList").When(x => x.TagList.HasValue);
        ArticleRules.Tags(RuleFor(x => x.TagList.Value)).OverridePropertyName("tagList").When(x => x.TagList.HasValue);
    }
}

public sealed class ArticleService(IAppDbContext db, IUniqueViolation unique, IClock clock)
{
    private sealed class ArticleRow
    {
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Tags { get; set; } = [];
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Favorited { get; set; }
        public int FavoritesCount { get; set; }
        public string AuthorUsername { get; set; } = "";
        public string? AuthorBio { get; set; }
        public string? AuthorImage { get; set; }
        public bool Following { get; set; }
    }

    // One projection = one SQL statement per list: no N+1 for tags, author, favorited, count, following.
    // me == 0 means anonymous (identity values start at 1).
    private static Expression<Func<Article, ArticleRow>> Row(int me) => a => new ArticleRow
    {
        Slug = a.Slug, Title = a.Title, Description = a.Description,
        Tags = a.Tags.OrderBy(t => t.Position).Select(t => t.Tag).ToList(),
        CreatedAt = a.CreatedAt, UpdatedAt = a.UpdatedAt,
        Favorited = a.Favorites.Any(f => f.UserId == me),
        FavoritesCount = a.Favorites.Count(),
        AuthorUsername = a.Author.Username, AuthorBio = a.Author.Bio, AuthorImage = a.Author.Image,
        Following = a.Author.Followers.Any(f => f.FollowerId == me),
    };

    private static ProfileDto AuthorOf(ArticleRow r) => new(r.AuthorUsername, r.AuthorBio, r.AuthorImage, r.Following);
    private static ArticleListItemDto ToItem(ArticleRow r) =>
        new(r.Slug, r.Title, r.Description, r.Tags, r.CreatedAt, r.UpdatedAt, r.Favorited, r.FavoritesCount, AuthorOf(r));
    private static ApiException NotFound() => new(404, "article", "not found");

    // ---- reads ----
    public Task<ArticleListDto> ListAsync(string? tag, string? author, string? favorited, string? offset, string? limit,
                                          int me, CancellationToken ct)
    {
        var page = Paging.Parse(offset, limit);
        IQueryable<Article> q = db.Articles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tag)) { var t = tag.Trim(); q = q.Where(a => a.Tags.Any(x => x.Tag == t)); }
        if (!string.IsNullOrWhiteSpace(author)) { var u = author.Trim(); q = q.Where(a => a.Author.Username == u); }
        if (!string.IsNullOrWhiteSpace(favorited)) { var u = favorited.Trim(); q = q.Where(a => a.Favorites.Any(f => f.User.Username == u)); }
        return RunAsync(q, page, me, ct);
    }

    public Task<ArticleListDto> FeedAsync(string? offset, string? limit, int me, CancellationToken ct)
    {
        var page = Paging.Parse(offset, limit);
        var q = db.Articles.AsNoTracking().Where(a => a.Author.Followers.Any(f => f.FollowerId == me));
        return RunAsync(q, page, me, ct);
    }

    private async Task<ArticleListDto> RunAsync(IQueryable<Article> q, Page page, int me, CancellationToken ct)
    {
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip(page.Offset).Take(page.Limit).Select(Row(me)).ToListAsync(ct);
        return new ArticleListDto(rows.Select(ToItem).ToList(), total);
    }

    public Task<ArticleDto> GetAsync(string slug, int me, CancellationToken ct) => GetViewAsync(slug, me, ct);

    private async Task<ArticleDto> GetViewAsync(string slug, int me, CancellationToken ct)
    {
        var row = await db.Articles.AsNoTracking().Where(a => a.Slug == slug).Select(Row(me)).FirstOrDefaultAsync(ct)
                  ?? throw NotFound();
        // body is fetched separately so list queries never load up to 20k chars per row
        var body = await db.Articles.AsNoTracking().Where(a => a.Slug == slug).Select(a => a.Body).FirstOrDefaultAsync(ct)
                   ?? throw NotFound();
        return new ArticleDto(row.Slug, row.Title, row.Description, body, row.Tags, row.CreatedAt, row.UpdatedAt,
                              row.Favorited, row.FavoritesCount, AuthorOf(row));
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(CancellationToken ct)
    {
        var rows = await db.ArticleTags.AsNoTracking().GroupBy(t => t.Tag)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Tag).Take(20).ToListAsync(ct);
        return rows.Select(x => x.Tag).ToList();
    }

    // ---- writes ----
    public async Task<ArticleDto> CreateAsync(int me, JsonElement body, CancellationToken ct)
    {
        var author = await db.Users.AsNoTracking().Where(u => u.Id == me)
            .Select(u => new { u.Username, u.Bio, u.Image }).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(401, "token", "is invalid");

        var r = new FieldReader(body);
        var input = new ArticleInput(r.String("title"), r.String("description"), r.String("body"), r.StringList("tagList"));
        Validate.Run(NewArticleValidator.Instance, input, r);

        var tags = input.TagList.Value ?? [];
        var now = clock.UtcNow;
        for (var attempt = 0; ; attempt++)
        {
            var article = new Article
            {
                Slug = await UniqueSlugAsync(input.Title.Value!, 0, ct),
                Title = input.Title.Value!, Description = input.Description.Value!, Body = input.Body.Value!,
                AuthorId = me, CreatedAt = now, UpdatedAt = now,
                Tags = tags.Select((t, i) => new ArticleTag { Tag = t, Position = i }).ToList(),
            };
            db.Articles.Add(article);
            try
            {
                await db.SaveChangesAsync(ct);
                return new ArticleDto(article.Slug, article.Title, article.Description, article.Body, tags,
                    now, now, false, 0, new ProfileDto(author.Username, author.Bio, author.Image, false));
            }
            catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex) && attempt < 3) { db.ChangeTracker.Clear(); }
            catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex))
            { throw new ApiException(409, "slug", "has already been taken"); }
        }
    }

    public async Task<ArticleDto> UpdateAsync(string slug, int me, JsonElement body, CancellationToken ct)
    {
        var article = await db.Articles.Include(a => a.Tags).FirstOrDefaultAsync(a => a.Slug == slug, ct)
                      ?? throw NotFound();
        if (article.AuthorId != me) throw new ApiException(403, "article", "forbidden");

        var r = new FieldReader(body);
        var input = new ArticleInput(r.String("title"), r.String("description"), r.String("body"), r.StringList("tagList"));
        Validate.Run(UpdateArticleValidator.Instance, input, r);

        var changed = false;
        if (input.Title.HasValue)
        {
            changed = true;
            if (input.Title.Value != article.Title)
            {
                article.Title = input.Title.Value!;
                article.Slug = await UniqueSlugAsync(article.Title, article.Id, ct);
            }
        }
        if (input.Description.HasValue) { article.Description = input.Description.Value!; changed = true; }
        if (input.Body.HasValue) { article.Body = input.Body.Value!; changed = true; }
        if (input.TagList.HasValue)
        {
            changed = true;
            var wanted = input.TagList.Value!;
            foreach (var old in article.Tags.Where(t => !wanted.Contains(t.Tag)).ToList()) db.ArticleTags.Remove(old);
            for (var i = 0; i < wanted.Count; i++)
            {
                var existing = article.Tags.FirstOrDefault(t => t.Tag == wanted[i]);
                if (existing is null) article.Tags.Add(new ArticleTag { Tag = wanted[i], Position = i });
                else existing.Position = i;
            }
        }
        if (changed) article.UpdatedAt = clock.UtcNow;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex))
        { throw new ApiException(409, "slug", "has already been taken"); }

        var newSlug = article.Slug;
        db.ChangeTracker.Clear();
        return await GetViewAsync(newSlug, me, ct);
    }

    public async Task DeleteAsync(string slug, int me, CancellationToken ct)
    {
        var a = await db.Articles.AsNoTracking().Where(x => x.Slug == slug)
            .Select(x => new { x.Id, x.AuthorId }).FirstOrDefaultAsync(ct) ?? throw NotFound();
        if (a.AuthorId != me) throw new ApiException(403, "article", "forbidden");

        // FKs are NoAction (SQL Server multiple-cascade-path rule), so children are removed explicitly in one transaction.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Comments.Where(c => c.ArticleId == a.Id).ExecuteDeleteAsync(ct);
        await db.Favorites.Where(f => f.ArticleId == a.Id).ExecuteDeleteAsync(ct);
        await db.ArticleTags.Where(t => t.ArticleId == a.Id).ExecuteDeleteAsync(ct);
        await db.Articles.Where(x => x.Id == a.Id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<ArticleDto> FavoriteAsync(string slug, int me, CancellationToken ct)
    {
        var id = await ArticleIdAsync(slug, ct);
        if (!await db.Favorites.AnyAsync(f => f.ArticleId == id && f.UserId == me, ct))
        {
            db.Favorites.Add(new Favorite { ArticleId = id, UserId = me, CreatedAt = clock.UtcNow });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex)) { db.ChangeTracker.Clear(); }
        }
        return await GetViewAsync(slug, me, ct);
    }

    public async Task<ArticleDto> UnfavoriteAsync(string slug, int me, CancellationToken ct)
    {
        var id = await ArticleIdAsync(slug, ct);
        await db.Favorites.Where(f => f.ArticleId == id && f.UserId == me).ExecuteDeleteAsync(ct);
        return await GetViewAsync(slug, me, ct);
    }

    private async Task<int> ArticleIdAsync(string slug, CancellationToken ct) =>
        await db.Articles.AsNoTracking().Where(a => a.Slug == slug).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw NotFound();

    private async Task<string> UniqueSlugAsync(string title, int excludeId, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Slugify(title);
        for (var n = 1; n < 10_000; n++)
        {
            var candidate = n == 1 ? baseSlug : SlugGenerator.WithSuffix(baseSlug, n);
            if (SlugGenerator.IsReserved(candidate)) continue;
            if (!await db.Articles.AnyAsync(a => a.Slug == candidate && a.Id != excludeId, ct)) return candidate;
        }
        throw new ApiException(409, "slug", "has already been taken");
    }
}
