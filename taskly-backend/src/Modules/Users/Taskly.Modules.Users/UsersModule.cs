using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Taskly.Modules.Users.Application;
using Taskly.Modules.Users.Domain;
using Taskly.Modules.Users.Infrastructure;
using Taskly.Modules.Users.Presentation;
using Taskly.SharedKernel.Infrastructure.Http;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.Modules.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, UsersModuleOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IPasswordHasher>(new BcryptPasswordHasher(options.BcryptRounds));
        services.AddSingleton<ITokenService>(provider => new JwtTokenService(
            options.AuthSecret,
            options.TokenTtl,
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IAvatarStorage>(provider => new LocalAvatarStorage(
            options.PublicDirectory,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<LocalAvatarStorage>>()));
        services.AddSingleton<IUserRepository, PostgresUserRepository>();

        services.AddSingleton<RegisterUser>();
        services.AddSingleton<AuthenticateUser>();
        services.AddSingleton<GetUser>();
        services.AddSingleton<ListUsers>();
        services.AddSingleton<UpdateUser>();
        services.AddSingleton<ChangeUserAvatar>();
        services.AddSingleton<DeleteUser>();
        services.AddSingleton<VerifyAccessToken>();

        services.AddSingleton(new AuthCookie(options.CookieName, options.TokenTtl, options.CookieDomain, options.Production));
        services.AddSingleton<IRequestAuthenticator, CookieRequestAuthenticator>();
        services.AddSingleton<IMigrationSource>(new EmbeddedMigrationSource(typeof(UsersModule).Assembly));

        return services;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapAuthEndpoints();
        routes.MapUsersManagementEndpoints();
        return routes;
    }
}
