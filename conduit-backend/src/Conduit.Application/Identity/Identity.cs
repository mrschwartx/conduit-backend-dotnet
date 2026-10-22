using System.Text.Json;
using System.Text.RegularExpressions;
using Conduit.Application.Abstractions;
using Conduit.Application.Common;
using Conduit.Domain.Identity;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Application.Identity;

public sealed record UserDto(string Email, string Token, string Username, string? Bio, string? Image);

public sealed record RegisterInput(Optional<string?> Username, Optional<string?> Email, Optional<string?> Password);
public sealed record LoginInput(Optional<string?> Email, Optional<string?> Password);
public sealed record UpdateUserInput(Optional<string?> Username, Optional<string?> Email, Optional<string?> Password,
                                     Optional<string?> Bio, Optional<string?> Image);

internal static class UserRules
{
    public static readonly Regex Username = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    // ASCII-only on purpose so a 254-char email always fits nvarchar(254) and the unique index.
    public static readonly Regex Email = new(
        @"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$",
        RegexOptions.Compiled);
}

public sealed class RegisterValidator : AbstractValidator<RegisterInput>
{
    public static readonly RegisterValidator Instance = new();
    public RegisterValidator()
    {
        RuleFor(x => x.Username.Value).Cascade(CascadeMode.Stop).Required().MinChars(3).MaxChars(30).Pattern(UserRules.Username).Named("username");
        RuleFor(x => x.Email.Value).Cascade(CascadeMode.Stop).Required().MaxChars(254).Pattern(UserRules.Email).Named("email");
        RuleFor(x => x.Password.Value).Cascade(CascadeMode.Stop).Required().MinChars(8).MaxChars(72).Named("password");
    }
}

public sealed class LoginValidator : AbstractValidator<LoginInput>
{
    public static readonly LoginValidator Instance = new();
    public LoginValidator()
    {
        RuleFor(x => x.Email.Value).Required().Named("email");
        RuleFor(x => x.Password.Value).Required().Named("password");
    }
}

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserInput>
{
    public static readonly UpdateUserValidator Instance = new();
    public UpdateUserValidator()
    {
        RuleFor(x => x.Username.Value).Cascade(CascadeMode.Stop).Required().MinChars(3).MaxChars(30).Pattern(UserRules.Username).Named("username").When(x => x.Username.HasValue);
        RuleFor(x => x.Email.Value).Cascade(CascadeMode.Stop).Required().MaxChars(254).Pattern(UserRules.Email).Named("email").When(x => x.Email.HasValue);
        RuleFor(x => x.Password.Value).Cascade(CascadeMode.Stop).Required().MinChars(8).MaxChars(72).Named("password").When(x => x.Password.HasValue);
        RuleFor(x => x.Bio.Value).MaxChars(300).Named("bio").When(x => x.Bio.HasValue);
        RuleFor(x => x.Image.Value).Cascade(CascadeMode.Stop).MaxChars(1000).HttpsUrlOrEmpty().Named("image").When(x => x.Image.HasValue);
    }
}

public sealed class IdentityService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens,
                                    IUniqueViolation unique, IClock clock)
{
    public async Task<UserDto> RegisterAsync(JsonElement body, CancellationToken ct)
    {
        var r = new FieldReader(body);
        var input = new RegisterInput(r.String("username"), r.String("email"), r.String("password", trim: false));
        Validate.Run(RegisterValidator.Instance, input, r);

        var email = input.Email.Value!.ToLowerInvariant();
        var username = input.Username.Value!;
        await EnsureUniqueAsync(email, username, 0, ct);

        var now = clock.UtcNow;
        var user = new User
        {
            Username = username, Email = email, PasswordHash = hasher.Hash(input.Password.Value!),
            CreatedAt = now, UpdatedAt = now
        };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex)) { throw ConflictFrom(ex); }
        return ToDto(user);
    }

    public async Task<UserDto> LoginAsync(JsonElement body, CancellationToken ct)
    {
        var r = new FieldReader(body);
        var input = new LoginInput(r.String("email"), r.String("password", trim: false));
        Validate.Run(LoginValidator.Instance, input, r);

        var email = input.Email.Value!.ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
        var ok = hasher.Verify(input.Password.Value!, user?.PasswordHash);
        if (user is null || !ok) throw new ApiException(401, "credentials", "are invalid");
        return ToDto(user);
    }

    public async Task<UserDto> GetCurrentAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new ApiException(401, "token", "is invalid");
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int userId, JsonElement body, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new ApiException(401, "token", "is invalid");

        var r = new FieldReader(body);
        var input = new UpdateUserInput(r.String("username"), r.String("email"), r.String("password", trim: false),
                                        r.String("bio"), r.String("image"));
        if (r.Errors.Count == 0 && !(input.Username.HasValue || input.Email.HasValue || input.Password.HasValue ||
                                     input.Bio.HasValue || input.Image.HasValue))
            throw new ApiException(422, "user", "must include at least one field");
        Validate.Run(UpdateUserValidator.Instance, input, r);

        var newEmail = input.Email.HasValue ? input.Email.Value!.ToLowerInvariant() : null;
        var newUsername = input.Username.HasValue ? input.Username.Value! : null;
        await EnsureUniqueAsync(newEmail == user.Email ? null : newEmail,
                                string.Equals(newUsername, user.Username, StringComparison.Ordinal) ? null : newUsername,
                                user.Id, ct);

        if (newUsername is not null) user.Username = newUsername;
        if (newEmail is not null) user.Email = newEmail;
        if (input.Password.HasValue) user.PasswordHash = hasher.Hash(input.Password.Value!);
        if (input.Bio.HasValue) user.Bio = NullIfEmpty(input.Bio.Value);
        if (input.Image.HasValue) user.Image = NullIfEmpty(input.Image.Value);
        user.UpdatedAt = clock.UtcNow;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex)) { throw ConflictFrom(ex); }
        return ToDto(user);
    }

    private async Task EnsureUniqueAsync(string? email, string? username, int excludeId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (email is not null && await db.Users.AnyAsync(u => u.Email == email && u.Id != excludeId, ct))
            errors["email"] = ["has already been taken"];
        if (username is not null && await db.Users.AnyAsync(u => u.Username == username && u.Id != excludeId, ct))
            errors["username"] = ["has already been taken"];
        if (errors.Count > 0) throw new ApiException(409, errors);
    }

    private static ApiException ConflictFrom(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return new ApiException(409, msg.Contains("Email", StringComparison.OrdinalIgnoreCase) ? "email" : "username",
                                "has already been taken");
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private UserDto ToDto(User u) => new(u.Email, tokens.Create(u.Id, u.Username), u.Username, u.Bio, u.Image);
}
