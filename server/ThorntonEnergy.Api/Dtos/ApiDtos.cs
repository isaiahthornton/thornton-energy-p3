using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Dtos;

// Auth. Login has no [EmailAddress] on purpose: a badly formed email is just a failed login (401), not a 400.
public record LoginRequest([Required] string Email, [Required] string Password);

public record RegisterRequest(
    [Required, RegularExpression(@"^TE-\d{6}$", ErrorMessage = "Account number looks like TE-123456.")] string AccountNumber,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password);

public record UserResponse(int Id, string Email, UserRole Role, int? CustomerId); // SessionUser in Angular
public record AuthResponse(string Token, DateTime ExpiresAt, UserResponse User);  // AuthResponse in Angular

// PATCH bodies: only the fields Angular actually changes
public record CustomerPatch(int? RatePlanId, AccountStatus? Status);
