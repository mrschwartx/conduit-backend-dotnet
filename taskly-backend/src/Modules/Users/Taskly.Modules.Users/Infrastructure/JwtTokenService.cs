using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Infrastructure;

public sealed class JwtTokenService : ITokenService
{
    private const string IdClaim = "id";

    private readonly SymmetricSecurityKey key;

    private readonly TimeSpan ttl;

    private readonly TimeProvider clock;

    private readonly JsonWebTokenHandler handler = new();

    public JwtTokenService(string secret, TimeSpan ttl, TimeProvider clock)
    {
        key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        this.ttl = ttl;
        this.clock = clock;
    }

    public string Issue(Guid userId)
    {
        var now = clock.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { [IdClaim] = userId.ToString() },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(ttl).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return handler.CreateToken(descriptor);
    }

    public async Task<Guid> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        TokenValidationResult result;
        try
        {
            result = await handler.ValidateTokenAsync(token, CreateParameters());
        }
        catch (Exception error) when (error is ArgumentException or SecurityTokenException)
        {
            throw new InvalidTokenException("Invalid token", error);
        }

        if (!result.IsValid)
        {
            throw new InvalidTokenException("Invalid token", result.Exception);
        }

        if (!result.Claims.TryGetValue(IdClaim, out var raw) || !EntityIds.TryParse(raw, out var userId))
        {
            throw new InvalidTokenException("Invalid token payload");
        }

        return userId;
    }

    private TokenValidationParameters CreateParameters() => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return expires.HasValue && expires.Value > now && (!notBefore.HasValue || notBefore.Value <= now);
        }
    };
}
