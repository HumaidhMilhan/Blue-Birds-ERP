using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.Security;

public sealed class RbacAuthorizationService : IRbacAuthorizationService
{
    private static readonly IReadOnlySet<RbacPermission> AdminPermissions =
        Enum.GetValues<RbacPermission>().ToHashSet();

    private static readonly IReadOnlySet<RbacPermission> CashierPermissions =
        new HashSet<RbacPermission>
        {
            RbacPermission.PosBilling,
            RbacPermission.PaymentRecording,
            RbacPermission.SalesReturns,
            RbacPermission.CustomerReadOnlyLookup
        };

    public bool HasPermission(UserRole role, RbacPermission permission)
    {
        return GetPermissions(role).Contains(permission);
    }

    public IReadOnlySet<RbacPermission> GetPermissions(UserRole role)
    {
        return role switch
        {
            UserRole.Admin => AdminPermissions,
            UserRole.Cashier => CashierPermissions,
            _ => new HashSet<RbacPermission>()
        };
    }
}
