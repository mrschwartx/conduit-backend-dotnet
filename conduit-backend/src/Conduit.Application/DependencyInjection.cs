using Conduit.Application.Articles;
using Conduit.Application.Comments;
using Conduit.Application.Identity;
using Conduit.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s) => s
        .AddScoped<IdentityService>()
        .AddScoped<ProfileService>()
        .AddScoped<ArticleService>()
        .AddScoped<CommentService>();
}
