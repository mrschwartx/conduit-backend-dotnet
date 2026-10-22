using Conduit.Application.Abstractions;
using Conduit.Application.Common;
using Conduit.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Application.Profiles;

public sealed record ProfileDto(string Username, string? Bio, string? Image, bool Following);

public sealed class ProfileService(IAppDbContext db, IUniqueViolation unique, IClock clock)
{
    private static ApiException NotFound() => new(404, "profile", "not found");

    public async Task<ProfileDto> GetAsync(string username, int me, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Username == username)
            .Select(u => new ProfileDto(u.Username, u.Bio, u.Image, u.Followers.Any(f => f.FollowerId == me)))
            .FirstOrDefaultAsync(ct) ?? throw NotFound();

    public async Task<ProfileDto> FollowAsync(string username, int me, CancellationToken ct)
    {
        var target = await db.Users.AsNoTracking().Where(u => u.Username == username)
            .Select(u => new { u.Id }).FirstOrDefaultAsync(ct) ?? throw NotFound();
        if (target.Id == me) throw new ApiException(422, "username", "you cannot follow yourself");

        if (!await db.Follows.AnyAsync(f => f.FollowerId == me && f.FolloweeId == target.Id, ct))
        {
            db.Follows.Add(new Follow { FollowerId = me, FolloweeId = target.Id, CreatedAt = clock.UtcNow });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex)) { db.ChangeTracker.Clear(); } // idempotent under races
        }
        return await GetAsync(username, me, ct);
    }

    public async Task<ProfileDto> UnfollowAsync(string username, int me, CancellationToken ct)
    {
        var target = await db.Users.AsNoTracking().Where(u => u.Username == username)
            .Select(u => new { u.Id }).FirstOrDefaultAsync(ct) ?? throw NotFound();
        await db.Follows.Where(f => f.FollowerId == me && f.FolloweeId == target.Id).ExecuteDeleteAsync(ct);
        return await GetAsync(username, me, ct);
    }
}
