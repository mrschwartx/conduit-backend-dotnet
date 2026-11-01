using Microsoft.AspNetCore.Http;

namespace Taskly.Modules.Users.Presentation;

public sealed class AuthCookie(string name, TimeSpan maxAge, string? domain, bool production)
{
    public string Name { get; } = name;

    public void Issue(HttpResponse response, string token) =>
        response.Cookies.Append(Name, token, Options(maxAge, null));

    public void Clear(HttpResponse response) =>
        response.Cookies.Append(Name, string.Empty, Options(TimeSpan.Zero, DateTimeOffset.UnixEpoch));

    private CookieOptions Options(TimeSpan age, DateTimeOffset? expires)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Path = "/",
            MaxAge = age,
            Expires = expires
        };

        if (production)
        {
            options.SameSite = SameSiteMode.None;
            options.Secure = true;
            if (!string.IsNullOrWhiteSpace(domain))
            {
                options.Domain = domain;
            }
        }
        else
        {
            options.SameSite = SameSiteMode.Lax;
            options.Secure = false;
        }

        return options;
    }
}
