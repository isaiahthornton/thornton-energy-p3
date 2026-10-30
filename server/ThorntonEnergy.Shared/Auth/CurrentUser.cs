using Microsoft.AspNetCore.Http;

namespace ThorntonEnergy.Shared.Auth;

// Who is making this request, and what may they see? Services use this for the ownership rules.
public interface ICurrentUser
{
    bool IsAdmin { get; }
    int? CustomerId { get; }
    string? Email { get; }
    bool CanAccessCustomer(int customerId);
    bool TryGetCustomerScope(int? requestedCustomerId, out int? scope);
}

// Reads the validated JWT of the current request through IHttpContextAccessor
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAdmin => Principal?.IsAdmin() ?? false;

    // The logged-in customer's id, read from the JWT (null for admins)
    public int? CustomerId => Principal?.GetCustomerId();

    public string? Email => Principal?.Identity?.IsAuthenticated == true ? Principal.GetEmail() : null;

    // Single-record rule: admins can reach any customer's data; customers only their own
    public bool CanAccessCustomer(int customerId) => IsAdmin || CustomerId == customerId;

    // List rule: which customer's rows may this request see?
    //   admin    -> whatever they asked for (null = everyone)
    //   customer -> always their own id; asking for someone else's returns false (refused)
    public bool TryGetCustomerScope(int? requestedCustomerId, out int? scope)
    {
        if (IsAdmin)
        {
            scope = requestedCustomerId;
            return true;
        }

        scope = CustomerId;
        return scope is not null && (requestedCustomerId is null || requestedCustomerId == scope);
    }
}
