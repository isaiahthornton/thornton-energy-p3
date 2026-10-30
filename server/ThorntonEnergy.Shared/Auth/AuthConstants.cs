namespace ThorntonEnergy.Shared.Auth;

// Custom claim names in our JWT (standard ones like "sub" and "email" come from JwtRegisteredClaimNames)
public static class AppClaims
{
    public const string Role = "role";
    public const string CustomerId = "customerId";
}

// Role names used in the token and in [Authorize(Roles = ...)]
public static class AppRoles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Customer = nameof(UserRole.Customer);

    // Not a person: one of our own services calling another when no user is signed in yet (login, register).
    // Only the monolith can mint these (it holds TokenService); see the Customers service's internal endpoints.
    public const string Service = "Service";
}

// Rate-limiting policy names used with [EnableRateLimiting(...)]
public static class RateLimitPolicies
{
    public const string PublicForms = "public-forms"; // login, register, contact form
}

public enum UserRole { Customer, Admin }