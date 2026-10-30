using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Services;

public record AuthToken(string Token, DateTime ExpiresAt);

// Controllers depend on this interface, not the class (SOLID "D"), so tests can swap in a fake
public interface ITokenService
{
    AuthToken CreateToken(User user);

    // A short-lived token with the "Service" role, for calls to other services before anyone is signed in
    AuthToken CreateServiceToken();
}
