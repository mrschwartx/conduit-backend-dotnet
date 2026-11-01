namespace Taskly.Modules.Users.Domain;

public interface IUserRepository
{
    Guid NextId();

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailOrUsernameAsync(
        string? email,
        string? username,
        Guid? excludeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> SearchAsync(
        string? username,
        string? email,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
