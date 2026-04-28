using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.Security;

public sealed class UserManagementService(
    ISecurityDataStore dataStore,
    ITransactionRunner transactionRunner,
    IPasswordHasher passwordHasher,
    ITemporaryPasswordGenerator passwordGenerator,
    ISessionService sessionService,
    IAuditLogger auditLogger,
    ISystemClock clock) : IUserManagementService
{
    public async Task<CashierAccountResult> CreateCashierAsync(
        CreateCashierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.AdminRole);
        var username = NormalizeUsername(request.Username);

        return await transactionRunner.ExecuteAsync(async token =>
        {
            if (await dataStore.UsernameExistsAsync(username, token))
            {
                throw new SecurityValidationException("Username already exists.");
            }

            var temporaryPassword = passwordGenerator.Generate();
            var user = new User
            {
                Username = username,
                PasswordHash = passwordHasher.HashPassword(temporaryPassword),
                Role = UserRole.Cashier,
                IsActive = true,
                CreatedAt = clock.Now,
                PasswordChangedAt = clock.Now
            };

            await dataStore.AddUserAsync(user, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.AdminUserId,
                request.AdminRole,
                "CASHIER_CREATE",
                "SECURITY",
                nameof(User),
                user.UserId,
                null,
                $"{{\"username\":\"{EscapeJson(user.Username)}\",\"role\":\"{user.Role}\",\"isActive\":true}}"), token);

            return new CashierAccountResult(user.UserId, user.Username, user.IsActive, temporaryPassword);
        }, cancellationToken);
    }

    public async Task<CashierAccountResult> DeactivateCashierAsync(
        DeactivateCashierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.AdminRole);

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var user = await GetRequiredCashierAsync(request.CashierUserId, token);
            var before = $"{{\"username\":\"{EscapeJson(user.Username)}\",\"isActive\":{JsonBool(user.IsActive)}}}";

            user.IsActive = false;
            user.DeactivatedAt = clock.Now;
            await dataStore.UpdateUserAsync(user, token);
            await sessionService.InvalidateUserSessionsAsync(user.UserId, token);

            await auditLogger.WriteAsync(new AuditEntry(
                request.AdminUserId,
                request.AdminRole,
                "CASHIER_DEACTIVATE",
                "SECURITY",
                nameof(User),
                user.UserId,
                before,
                $"{{\"username\":\"{EscapeJson(user.Username)}\",\"isActive\":false}}"), token);

            return new CashierAccountResult(user.UserId, user.Username, user.IsActive);
        }, cancellationToken);
    }

    public async Task<PasswordResetResult> ResetCashierPasswordAsync(
        ResetCashierPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.AdminRole);

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var user = await GetRequiredCashierAsync(request.CashierUserId, token);
            if (!user.IsActive)
            {
                throw new SecurityValidationException("Inactive Cashier accounts cannot have passwords reset.");
            }

            var temporaryPassword = passwordGenerator.Generate();
            var before = $"{{\"username\":\"{EscapeJson(user.Username)}\",\"passwordChangedAt\":\"{user.PasswordChangedAt:O}\"}}";
            user.PasswordHash = passwordHasher.HashPassword(temporaryPassword);
            user.PasswordChangedAt = clock.Now;
            await dataStore.UpdateUserAsync(user, token);

            await auditLogger.WriteAsync(new AuditEntry(
                request.AdminUserId,
                request.AdminRole,
                "CASHIER_PASSWORD_RESET",
                "SECURITY",
                nameof(User),
                user.UserId,
                before,
                $"{{\"username\":\"{EscapeJson(user.Username)}\",\"passwordChangedAt\":\"{user.PasswordChangedAt:O}\"}}"), token);

            return new PasswordResetResult(
                user.UserId,
                user.Username,
                temporaryPassword,
                user.PasswordChangedAt!.Value);
        }, cancellationToken);
    }

    private async Task<User> GetRequiredCashierAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new SecurityValidationException("Cashier user is required.");
        }

        var user = await dataStore.GetUserAsync(userId, cancellationToken)
            ?? throw new SecurityValidationException("Cashier account was not found.");

        if (user.Role != UserRole.Cashier)
        {
            throw new SecurityValidationException("Only Cashier accounts can be managed here.");
        }

        return user;
    }

    private static void EnsureAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            throw new SecurityValidationException("Only Admin users can manage Cashier accounts.");
        }
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new SecurityValidationException("Username is required.");
        }

        return username.Trim();
    }

    private static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
