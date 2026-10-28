using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService auth) : AppControllerBase
{
    // POST api/auth/login: check the password, then hand back a signed JWT
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicForms)] // slows down password guessing
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
        FromResult(await auth.LoginAsync(request));

    // POST api/auth/register: link a new login to an existing customer, then sign them in
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicForms)]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request) =>
        FromResult(await auth.RegisterAsync(request));

    // GET api/auth/me: who does this token belong to? 401 without a valid token.
    [Authorize]
    [HttpGet("me")]
    public ActionResult<UserResponse> Me() =>
        new UserResponse(User.GetUserId(), User.GetEmail(), User.GetRole(), User.GetCustomerId());
}
