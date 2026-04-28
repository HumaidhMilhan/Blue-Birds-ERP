using System.Security.Cryptography;
using System.Text;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Application.Security;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Configuration;

namespace BlueBirdsERP.Infrastructure.Security;

public sealed class RecoveryAccessService(
    InfrastructureOptions options,
    ISecurityDataStore dataStore,
    IPasswordHasher passwordHasher,
    ITemporaryPasswordGenerator passwordGenerator,
    IAuditLogger auditLogger,
    ISystemClock clock) : IRecoveryAccessService
{
    public async Task<RecoveryAccessResult> GenerateTemporaryAdminPasswordAsync(
        RecoveryAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!options.Recovery.Enabled || string.IsNullOrWhiteSpace(options.Recovery.RecoveryKeySha256))
        {
            throw new SecurityValidationException("Recovery access is not enabled.");
        }

        if (!string.Equals(HashRecoveryKey(request.RecoveryKey), options.Recovery.RecoveryKeySha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityValidationException("Recovery key is invalid.");
        }

        var username = string.IsNullOrWhiteSpace(request.TargetAdminUsername)
            ? options.Recovery.DefaultRecoveryAdminUsername
            : request.TargetAdminUsername.Trim();
        var user = await dataStore.GetUserByUsernameAsync(username, cancellationToken);

        if (user is not null && user.Role != UserRole.Admin)
        {
            throw new SecurityValidationException("Recovery can only reset Admin accounts.");
        }

        var before = user is null
            ? null
            : $"{{\"username\":\"{user.Username}\",\"isActive\":{JsonBool(user.IsActive)},\"passwordChangedAt\":\"{user.PasswordChangedAt:O}\"}}";
        var temporaryPassword = passwordGenerator.Generate();

        if (user is null)
        {
            user = new User
            {
                Username = username,
                Role = UserRole.Admin,
                CreatedAt = clock.Now
            };
            await dataStore.AddUserAsync(user, cancellationToken);
        }

        user.IsActive = true;
        user.DeactivatedAt = null;
        user.PasswordHash = passwordHasher.HashPassword(temporaryPassword);
        user.PasswordChangedAt = clock.Now;
        await dataStore.UpdateUserAsync(user, cancellationToken);

        await auditLogger.WriteAsync(new AuditEntry(
            Guid.Empty,
            UserRole.Admin,
            "BREAK_GLASS_ADMIN_RECOVERY",
            "SECURITY",
            nameof(User),
            user.UserId,
            before,
            $"{{\"username\":\"{user.Username}\",\"isActive\":true,\"passwordChangedAt\":\"{user.PasswordChangedAt:O}\"}}"), cancellationToken);

        return new RecoveryAccessResult(
            user.UserId,
            user.Username,
            temporaryPassword,
            user.PasswordChangedAt!.Value);
    }

    private static string HashRecoveryKey(string recoveryKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recoveryKey)));
    }

    private static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }
}
