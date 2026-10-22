using System.Globalization;
using Conduit.Application.Abstractions;
using Conduit.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Conduit.Infrastructure;

public sealed record JwtOptions(string Secret, int TtlMinutes);

public sealed class SystemClock : IClock
{
    public DateTime UtcNow
    {
        get { var n = DateTime.UtcNow; return new DateTime(n.Ticks - n.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc); }
    }
}

public sealed class SqlUniqueViolation : IUniqueViolation
{
    public bool IsUniqueViolation(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is SqlException s && s.Number is 2601 or 2627) return true;
        return false;
    }
}

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int Cost = 11;
    private static readonly string Dummy = BCrypt.Net.BCrypt.EnhancedHashPassword("dummy-password", Cost);

    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, Cost);

    public bool Verify(string password, string? hash)
    {
        var ok = BCrypt.Net.BCrypt.EnhancedVerify(password, hash ?? Dummy);
        return ok && hash is not null;
    }
}

public sealed class JwtTokenService : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SymmetricSecurityKey _key;
    private readonly int _ttl;
    private readonly IClock _clock;
    private readonly TokenValidationParameters _params;

    public JwtTokenService(JwtOptions options, IClock clock)
    {
        _clock = clock;
        _ttl = options.TtlMinutes;
        _key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.Secret));
        _params = new TokenValidationParameters
        {
            IssuerSigningKey = _key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
        };
    }

    public string Create(int userId, string username)
    {
        var now = _clock.UtcNow;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(CultureInfo.InvariantCulture),
                ["username"] = username,
            },
            IssuedAt = now,
            Expires = now.AddMinutes(_ttl),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        });
    }

    public async Task<TokenValidation> ValidateAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, _params);
        if (!result.IsValid)
            return new(false, 0, null, result.Exception is SecurityTokenExpiredException ? "has expired" : "is invalid");
        var jwt = (JsonWebToken)result.SecurityToken;
        if (!int.TryParse(jwt.Subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            return new(false, 0, null, "is invalid");
        jwt.TryGetPayloadValue<string>("username", out var username);
        return new(true, id, username, null);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, string connectionString, JwtOptions jwt)
    {
        s.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
        s.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        s.AddSingleton(jwt);
        s.AddSingleton<IClock, SystemClock>();
        s.AddSingleton<IUniqueViolation, SqlUniqueViolation>();
        s.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        s.AddSingleton<ITokenService, JwtTokenService>();
        return s;
    }
}
