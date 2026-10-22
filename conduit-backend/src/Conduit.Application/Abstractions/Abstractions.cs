using Conduit.Domain.Articles;
using Conduit.Domain.Comments;
using Conduit.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Conduit.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Follow> Follows { get; }
    DbSet<Article> Articles { get; }
    DbSet<ArticleTag> ArticleTags { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Comment> Comments { get; }
    ChangeTracker ChangeTracker { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    /// <summary>Always does the slow work, even when hash is null, so timing does not reveal unknown emails.</summary>
    bool Verify(string password, string? hash);
}

public sealed record TokenValidation(bool IsValid, int UserId, string? Username, string? Error);

public interface ITokenService
{
    string Create(int userId, string username);
    Task<TokenValidation> ValidateAsync(string token);
}

public interface IUniqueViolation { bool IsUniqueViolation(Exception ex); }

public interface IClock { DateTime UtcNow { get; } }
