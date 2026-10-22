using Conduit.Domain.Comments;
using Conduit.Domain.Identity;

namespace Conduit.Domain.Articles;

public class Article
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Body { get; set; } = "";
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ArticleTag> Tags { get; set; } = [];
    public List<Favorite> Favorites { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
}

public class ArticleTag
{
    public int ArticleId { get; set; }
    public string Tag { get; set; } = "";
    public int Position { get; set; }
}

public class Favorite
{
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
