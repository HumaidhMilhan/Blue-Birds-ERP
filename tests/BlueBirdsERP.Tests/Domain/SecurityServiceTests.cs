using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Application.Security;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Tests.Domain;

public sealed class SecurityServiceTests
{
    [Fact]
    public void Permission_matrix_allows_admin_full_access_and_restricts_cashier()
    {
        var service = new RbacAuthorizationService();

        Assert.True(Enum.GetValues<RbacPermission>().All(permission => service.HasPermission(UserRole.Admin, permission)));
        Assert.True(service.HasPermission(UserRole.Cashier, RbacPermission.PosBilling));
        Assert.True(service.HasPermission(UserRole.Cashier, RbacPermission.PaymentRecording));
        Assert.True(service.HasPermission(UserRole.Cashier, RbacPermission.SalesReturns));
        Assert.True(service.HasPermission(UserRole.Cashier, RbacPermission.CustomerReadOnlyLookup));
        Assert.False(service.HasPermission(UserRole.Cashier, RbacPermission.InventoryManagement));
        Assert.False(service.HasPermission(UserRole.Cashier, RbacPermission.WhatsAppConfiguration));
        Assert.False(service.HasPermission(UserRole.Cashier, RbacPermission.UserManagement));
        Assert.False(service.HasPermission(UserRole.Cashier, RbacPermission.SystemConfiguration));
    }

    [Fact]
    public async Task Authentication_accepts_active_user_and_rejects_inactive_or_wrong_password()
    {
        var harness = CreateHarness();
        var activeCashier = harness.Store.AddUser("cashier1", "Temp123", UserRole.Cashier, isActive: true);
        harness.Store.AddUser("inactive", "Temp123", UserRole.Cashier, isActive: false);

        var authenticated = await harness.AuthenticationService.SignInAsync("cashier1", "Temp123");
        var wrongPassword = await harness.AuthenticationService.SignInAsync("cashier1", "bad-password");
        var inactive = await harness.AuthenticationService.SignInAsync("inactive", "Temp123");

        Assert.NotNull(authenticated);
        Assert.Equal(activeCashier.UserId, authenticated.UserId);
        Assert.Equal(harness.Clock.Now, activeCashier.LastLogin);
        Assert.Null(wrongPassword);
        Assert.Null(inactive);
    }

    [Fact]
    public async Task Admin_can_create_cashier_with_generated_temporary_password()
    {
        var harness = CreateHarness();
        harness.PasswordGenerator.NextPasswords.Enqueue("Generated-001");

        var result = await harness.UserManagementService.CreateCashierAsync(new CreateCashierRequest(
            " cashier1 ",
            harness.AdminId,
            UserRole.Admin));

        var cashier = harness.Store.Users.Single(user => user.UserId == result.UserId);
        Assert.Equal("cashier1", result.Username);
        Assert.Equal("Generated-001", result.TemporaryPassword);
        Assert.Equal(UserRole.Cashier, cashier.Role);
        Assert.True(cashier.IsActive);
        Assert.Equal("HASH:Generated-001", cashier.PasswordHash);
        Assert.Equal(harness.Clock.Now, cashier.PasswordChangedAt);
        Assert.Single(harness.AuditLogger.Entries);
        Assert.Equal("CASHIER_CREATE", harness.AuditLogger.Entries[0].Action);
    }

    [Fact]
    public async Task Cashier_cannot_manage_cashier_accounts()
    {
        var harness = CreateHarness();

        await Assert.ThrowsAsync<SecurityValidationException>(() => harness.UserManagementService.CreateCashierAsync(new CreateCashierRequest(
            "cashier1",
            Guid.NewGuid(),
            UserRole.Cashier)));
    }

    [Fact]
    public async Task Admin_cannot_deactivate_or_reset_admin_account_from_cashier_management()
    {
        var harness = CreateHarness();
        var admin = harness.Store.AddUser("admin2", "Admin123", UserRole.Admin, isActive: true);

        await Assert.ThrowsAsync<SecurityValidationException>(() => harness.UserManagementService.DeactivateCashierAsync(new DeactivateCashierRequest(
            admin.UserId,
            harness.AdminId,
            UserRole.Admin)));
        await Assert.ThrowsAsync<SecurityValidationException>(() => harness.UserManagementService.ResetCashierPasswordAsync(new ResetCashierPasswordRequest(
            admin.UserId,
            harness.AdminId,
            UserRole.Admin)));
    }

    [Fact]
    public async Task Deactivating_cashier_invalidates_active_session_and_audits()
    {
        var harness = CreateHarness();
        var cashier = harness.Store.AddUser("cashier1", "Temp123", UserRole.Cashier, isActive: true);
        await harness.SessionService.BeginSessionAsync(new AuthenticatedUser(cashier.UserId, cashier.Username, cashier.Role));

        var result = await harness.UserManagementService.DeactivateCashierAsync(new DeactivateCashierRequest(
            cashier.UserId,
            harness.AdminId,
            UserRole.Admin));
        var isSessionActive = await harness.SessionService.IsSessionActiveAsync(cashier.UserId);

        Assert.False(result.IsActive);
        Assert.False(cashier.IsActive);
        Assert.Equal(harness.Clock.Now, cashier.DeactivatedAt);
        Assert.False(isSessionActive);
        Assert.Single(harness.AuditLogger.Entries);
        Assert.Equal("CASHIER_DEACTIVATE", harness.AuditLogger.Entries[0].Action);
    }

    [Fact]
    public async Task Reset_cashier_password_generates_new_password_and_updates_hash()
    {
        var harness = CreateHarness();
        var cashier = harness.Store.AddUser("cashier1", "Old123", UserRole.Cashier, isActive: true);
        harness.PasswordGenerator.NextPasswords.Enqueue("Generated-Reset");

        var result = await harness.UserManagementService.ResetCashierPasswordAsync(new ResetCashierPasswordRequest(
            cashier.UserId,
            harness.AdminId,
            UserRole.Admin));

        Assert.Equal("Generated-Reset", result.TemporaryPassword);
        Assert.Equal("HASH:Generated-Reset", cashier.PasswordHash);
        Assert.Equal(harness.Clock.Now, result.PasswordChangedAt);
        Assert.Single(harness.AuditLogger.Entries);
        Assert.Equal("CASHIER_PASSWORD_RESET", harness.AuditLogger.Entries[0].Action);
    }

    private static TestHarness CreateHarness()
    {
        return new TestHarness();
    }

    private sealed class TestHarness
    {
        public Guid AdminId { get; } = Guid.NewGuid();
        public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 4, 29, 11, 0, 0, TimeSpan.Zero));
        public InMemorySecurityDataStore Store { get; } = new();
        public FakePasswordHasher PasswordHasher { get; } = new();
        public QueuePasswordGenerator PasswordGenerator { get; } = new();
        public InMemorySessionService SessionService { get; }
        public RecordingAuditLogger AuditLogger { get; } = new();
        public AuthenticationService AuthenticationService { get; }
        public UserManagementService UserManagementService { get; }

        public TestHarness()
        {
            SessionService = new InMemorySessionService(Clock);
            AuthenticationService = new AuthenticationService(Store, PasswordHasher, Clock);
            UserManagementService = new UserManagementService(
                Store,
                new InlineTransactionRunner(),
                PasswordHasher,
                PasswordGenerator,
                SessionService,
                AuditLogger,
                Clock);
        }
    }

    private sealed class InMemorySecurityDataStore : ISecurityDataStore
    {
        private readonly Dictionary<Guid, User> usersById = [];
        private readonly Dictionary<string, User> usersByUsername = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<User> Users => usersById.Values;

        public User AddUser(string username, string password, UserRole role, bool isActive)
        {
            var user = new User
            {
                Username = username,
                PasswordHash = $"HASH:{password}",
                Role = role,
                IsActive = isActive,
                CreatedAt = new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero)
            };
            usersById[user.UserId] = user;
            usersByUsername[user.Username] = user;
            return user;
        }

        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            usersById.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            usersByUsername.TryGetValue(username, out var user);
            return Task.FromResult(user);
        }

        public Task<IReadOnlyList<User>> GetUsersByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<User> result = usersById.Values
                .Where(user => user.Role == role)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(usersByUsername.ContainsKey(username));
        }

        public Task AddUserAsync(User user, CancellationToken cancellationToken = default)
        {
            usersById[user.UserId] = user;
            usersByUsername[user.Username] = user;
            return Task.CompletedTask;
        }

        public Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
        {
            usersById[user.UserId] = user;
            usersByUsername[user.Username] = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            return $"HASH:{password}";
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            return passwordHash == $"HASH:{password}";
        }
    }

    private sealed class QueuePasswordGenerator : ITemporaryPasswordGenerator
    {
        public Queue<string> NextPasswords { get; } = [];

        public string Generate()
        {
            return NextPasswords.Count == 0 ? "Generated-Default" : NextPasswords.Dequeue();
        }
    }

    private sealed class InlineTransactionRunner : ITransactionRunner
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset Now { get; } = now;
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
