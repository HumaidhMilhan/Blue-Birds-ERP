using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Application.Security;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Configuration;

namespace BlueBirdsERP.Infrastructure.Security;

public sealed class DevelopmentBootstrapService(
    InfrastructureOptions options,
    ISecurityDataStore dataStore,
    IPasswordHasher passwordHasher,
    IAuditLogger auditLogger,
    ISystemClock clock) : IApplicationBootstrapService
{
    public async Task<BootstrapResult> EnsureDevelopmentBootstrapAsync(CancellationToken cancellationToken = default)
    {
        if (!options.DevelopmentBootstrap.Enabled ||
            string.Equals(options.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(options.DevelopmentBootstrap.Username) ||
            string.IsNullOrWhiteSpace(options.DevelopmentBootstrap.Password))
        {
            return new BootstrapResult(false, options.DevelopmentBootstrap.Username);
        }

        var username = options.DevelopmentBootstrap.Username.Trim();
        if (await dataStore.UsernameExistsAsync(username, cancellationToken))
        {
            return new BootstrapResult(false, username);
        }

        var user = new User
        {
            Username = username,
            PasswordHash = passwordHasher.HashPassword(options.DevelopmentBootstrap.Password),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = clock.Now,
            PasswordChangedAt = clock.Now
        };

        await dataStore.AddUserAsync(user, cancellationToken);
        await auditLogger.WriteAsync(new AuditEntry(
            Guid.Empty,
            UserRole.Admin,
            "DEV_BOOTSTRAP_ADMIN_CREATE",
            "SECURITY",
            nameof(User),
            user.UserId,
            null,
            $"{{\"username\":\"{user.Username}\",\"environment\":\"{options.EnvironmentName}\"}}"), cancellationToken);

        return new BootstrapResult(true, username);
    }
}
