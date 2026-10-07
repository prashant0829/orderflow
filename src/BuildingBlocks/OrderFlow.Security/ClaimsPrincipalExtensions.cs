using System.Security.Claims;

namespace OrderFlow.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The user id issued by the Identity service (the "sub" claim). Also used as the customer id.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : throw new InvalidOperationException("The token has no valid user id.");
    }

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

    /// <summary>True when the caller owns the resource or is an admin.</summary>
    public static bool CanAccessCustomer(this ClaimsPrincipal user, Guid customerId) => user.IsAdmin() || user.GetUserId() == customerId;
}
