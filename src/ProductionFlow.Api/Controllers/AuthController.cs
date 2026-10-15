
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductionFlow.Api.Dtos;
using ProductionFlow.Api.Identity;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAntiforgery _antiforgery;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAntiforgery antiforgery)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _antiforgery = antiforgery;
    }

    // GET /api/auth/csrf-token
    [HttpGet("csrf-token")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(
            HttpContext);

        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new
        {
            RequestToken = tokens.RequestToken
        });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserResponse>> Login(
        [FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        var response = await CreateUserResponseAsync(user);

        return Ok(response);
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var response = await CreateUserResponseAsync(user);

        return Ok(response);
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return NoContent();
    }

    private async Task<CurrentUserResponse>
        CreateUserResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.OrderBy(role => role).ToList());
    }

    private UnauthorizedObjectResult InvalidCredentials()
    {
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Nieprawidłowe dane logowania.",
            Detail = "Nieprawidłowy e-mail lub hasło."
        });
    }
}
