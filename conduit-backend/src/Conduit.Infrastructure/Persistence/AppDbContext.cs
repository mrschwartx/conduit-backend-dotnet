using Conduit.Application.Abstractions;
using Conduit.Domain.Articles;
using Conduit.Domain.Comments;
using Conduit.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Conduit.Infrastructure.Persistence;

public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    private const string CaseInsensitive = "Latin1_General_100_CI_AS";
    private const string Binary = "Latin1_General_100_BIN2";

    public DbSet<User> Users => Set<User>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b) =>
        b.Properties<DateTime>().HaveColumnType("datetime2(3)").HaveConversion<UtcDateTimeConverter>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.Username).HasMaxLength(30).UseCollation(CaseInsensitive);
            e.Property(x => x.Email).HasMaxLength(254).UseCollation(CaseInsensitive);   // stored lower-case too
            e.Property(x => x.PasswordHash).HasMaxLength(100);
            e.Property(x => x.Bio).HasMaxLength(600);       // 300 chars may be 600 UTF-16 units
            e.Property(x => x.Image).HasMaxLength(2000);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
        });

        b.Entity<Follow>(e =>
        {
            e.ToTable("Follows");
            e.HasKey(x => new { x.FollowerId, x.FolloweeId });
            e.HasOne(x => x.Follower).WithMany(u => u.Following).HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Followee).WithMany(u => u.Followers).HasForeignKey(x => x.FolloweeId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.FolloweeId);
        });

        b.Entity<Article>(e =>
        {
            e.ToTable("Articles");
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(400);
            e.Property(x => x.Body).HasColumnType("nvarchar(max)");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.CreatedAt, x.Id });
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<ArticleTag>(e =>
        {
            e.ToTable("ArticleTags");
            e.HasKey(x => new { x.ArticleId, x.Tag });
            e.Property(x => x.Tag).HasMaxLength(60).UseCollation(Binary);   // exact, case-sensitive tag matching
            e.HasOne<Article>().WithMany(a => a.Tags).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.Tag);
        });

        b.Entity<Favorite>(e =>
        {
            e.ToTable("Favorites");
            e.HasKey(x => new { x.ArticleId, x.UserId });
            e.HasOne(x => x.Article).WithMany(a => a.Favorites).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<Comment>(e =>
        {
            e.ToTable("Comments");
            e.Property(x => x.Body).HasMaxLength(2000);
            e.HasOne(x => x.Article).WithMany(a => a.Comments).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.ArticleId, x.CreatedAt, x.Id });
        });
    }
}

/// <summary>Used only by `dotnet ef`; does not need JWT_SECRET etc.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                 ?? "Server=localhost,1433;Database=conduit;User Id=sa;Password=Dev_Passw0rd123;TrustServerCertificate=True";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
    }
}
