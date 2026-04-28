using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;

namespace BlueBirdsERP.Application.Security;

public sealed class InMemorySessionService(ISystemClock clock) : ISessionService
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, SessionState> sessionsByUser = [];
    private Guid? currentUserId;

    public TimeSpan InactivityTimeout { get; } = TimeSpan.Zero;

    public Task BeginSessionAsync(AuthenticatedUser user, CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            sessionsByUser[user.UserId] = new SessionState(user, clock.Now, IsActive: true);
            currentUserId = user.UserId;
        }

        return Task.CompletedTask;
    }

    public Task TouchAsync(CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            if (currentUserId.HasValue &&
                sessionsByUser.TryGetValue(currentUserId.Value, out var session) &&
                session.IsActive)
            {
                sessionsByUser[currentUserId.Value] = session with { LastActivityAt = clock.Now };
            }
        }

        return Task.CompletedTask;
    }

    public Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            if (currentUserId.HasValue &&
                sessionsByUser.TryGetValue(currentUserId.Value, out var session))
            {
                sessionsByUser[currentUserId.Value] = session with { LastActivityAt = clock.Now, IsActive = false };
            }

            currentUserId = null;
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsSessionActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            return Task.FromResult(sessionsByUser.TryGetValue(userId, out var session) && session.IsActive);
        }
    }

    public Task InvalidateUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            if (sessionsByUser.TryGetValue(userId, out var session))
            {
                sessionsByUser[userId] = session with { LastActivityAt = clock.Now, IsActive = false };
            }

            if (currentUserId == userId)
            {
                currentUserId = null;
            }
        }

        return Task.CompletedTask;
    }

    private sealed record SessionState(
        AuthenticatedUser User,
        DateTimeOffset LastActivityAt,
        bool IsActive);
}
