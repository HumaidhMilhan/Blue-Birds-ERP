using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;

namespace BlueBirdsERP.Application.Security;

public sealed class AuthenticationService(
    ISecurityDataStore dataStore,
    IPasswordHasher passwordHasher,
    ISystemClock clock) : IAuthenticationService
{
    public async Task<AuthenticatedUser?> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await dataStore.GetUserByUsernameAsync(username.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        if (!passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        user.LastLogin = clock.Now;
        await dataStore.UpdateUserAsync(user, cancellationToken);

        return new AuthenticatedUser(user.UserId, user.Username, user.Role);
    }
}
