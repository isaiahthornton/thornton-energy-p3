using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Api.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request);
}

public class AuthService(
    IRepository<User> users,
    ICustomerAccounts accounts, // customers live in the Customers service; asked with a service token
    ITokenService tokens,
    ILogger<AuthService> logger) : IAuthService
{
    // Check the password, then hand back a signed JWT
    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await users.Query().FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // Log who tried, never the password they typed
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");
        }

        // Suspended or closed customers cannot sign in. The server enforces this, not just the browser.
        // (Admins have no customer account, so they skip the call and can sign in even if Customers is down.)
        if (user.CustomerId is int customerId)
        {
            var account = await accounts.GetAsync(customerId);
            var status = account?.Status ?? AccountStatus.Closed; // a login whose customer no longer exists counts as closed
            if (status != AccountStatus.Active)
            {
                logger.LogWarning("Login blocked for {Email}: account is {Status}", request.Email, status);
                return Result<AuthResponse>.Forbidden(AccountNotActiveMessage(status));
            }
        }

        logger.LogInformation("User {UserId} signed in", user.Id);
        return Result<AuthResponse>.Success(ToAuthResponse(user));
    }

    // Link a new login to an existing customer, then sign them in
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        var customer = await accounts.FindAsync(request.AccountNumber, request.Email);
        if (customer is null)
            return Result<AuthResponse>.NotFound("Customer not found with provided account number and email.");

        if (customer.Status != AccountStatus.Active)
            return Result<AuthResponse>.Forbidden(AccountNotActiveMessage(customer.Status));

        var exists = await users.Query().AnyAsync(u => u.CustomerId == customer.Id || u.Email == request.Email);
        if (exists)
            return Result<AuthResponse>.Conflict("A user account already exists for this customer.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Customer,
            CustomerId = customer.Id,
        };
        users.Add(user);
        await users.SaveChangesAsync();

        logger.LogInformation("User {UserId} registered for customer {CustomerId}", user.Id, customer.Id);
        return Result<AuthResponse>.Success(ToAuthResponse(user));
    }

    private AuthResponse ToAuthResponse(User user)
    {
        var token = tokens.CreateToken(user);
        // Never send the password hash back to the browser
        return new AuthResponse(token.Token, token.ExpiresAt, new UserResponse(user.Id, user.Email, user.Role, user.CustomerId));
    }

    private static string AccountNotActiveMessage(AccountStatus status) =>
        $"This account is {status.ToString().ToLowerInvariant()}. Please call customer care at 1-800-555-0100.";
}
