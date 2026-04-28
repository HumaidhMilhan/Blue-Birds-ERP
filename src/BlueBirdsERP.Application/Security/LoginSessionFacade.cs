using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.Security;

public sealed class LoginSessionFacade(
    IAuthenticationService authenticationService,
    ISessionService sessionService,
    IRbacAuthorizationService authorizationService) : ILoginSessionFacade
{
    public AuthenticatedUser? CurrentUser { get; private set; }

    public IReadOnlySet<RbacPermission> CurrentPermissions =>
        CurrentUser is null ? new HashSet<RbacPermission>() : authorizationService.GetPermissions(CurrentUser.Role);

    public async Task<LoginResult> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await authenticationService.SignInAsync(request.Username, request.Password, cancellationToken);
        if (user is null)
        {
            return new LoginResult(false, null, new HashSet<RbacPermission>(), "Invalid username or password.");
        }

        CurrentUser = user;
        await sessionService.BeginSessionAsync(user, cancellationToken);

        return new LoginResult(true, user, authorizationService.GetPermissions(user.Role), null);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await sessionService.EndSessionAsync(cancellationToken);
        CurrentUser = null;
    }
}
