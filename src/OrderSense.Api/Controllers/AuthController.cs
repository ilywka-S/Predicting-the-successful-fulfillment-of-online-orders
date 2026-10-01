using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    TokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        
        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        
        if (result.IsLockedOut)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Account is temporarily locked after too many failed attempts");
        }

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        return await tokenService.IssueAsync(user, ct);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var user = await tokenService.RedeemAsync(request.RefreshToken, ct);
        
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid refresh token");
        }

        return await tokenService.IssueAsync(user, ct);
    }
    
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await tokenService.RevokeAsync(request.RefreshToken, ct);
        
        return NoContent();
    }
    
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        
        if (user is null)
        {
            return Unauthorized();
        }

        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "";
        
        return TokenService.ToUserDto(user, role);
    }

    private ObjectResult InvalidCredentials() => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password");
}