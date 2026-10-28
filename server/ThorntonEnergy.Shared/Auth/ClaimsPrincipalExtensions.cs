using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ThorntonEnergy.Shared.Auth;

// Helpers to read the logged-in user's details from the validated token (the User property in a controller)
public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public static string GetEmail(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Email)!;

    public static UserRole GetRole(this ClaimsPrincipal user) =>
        Enum.Parse<UserRole>(user.FindFirstValue(AppClaims.Role)!);

    // Admins have no customer account, so this is null for them
    public static int? GetCustomerId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(AppClaims.CustomerId), out var id) ? id : null;

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(AppRoles.Admin);
}