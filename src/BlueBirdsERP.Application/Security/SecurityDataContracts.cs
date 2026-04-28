using BlueBirdsERP.Domain.Entities;

namespace BlueBirdsERP.Application.Security;

public interface ISecurityDataStore
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersByRoleAsync(BlueBirdsERP.Domain.Enums.UserRole role, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task AddUserAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public interface ITemporaryPasswordGenerator
{
    string Generate();
}

public sealed class SecurityValidationException(string message) : InvalidOperationException(message);
