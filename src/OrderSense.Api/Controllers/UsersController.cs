using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Auth;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db, UserManager<AppUser> userManager) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<UserDto>>(StatusCodes.Status200OK)]
    public async Task<List<UserDto>> GetAll(CancellationToken ct)
    {
        return await db.Users
            .OrderBy(u => u.Email)
            .Select(u => new UserDto(
                u.Id,
                u.Email!,
                u.FullName,
                db.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                    .FirstOrDefault() ?? ""))
            .ToListAsync(ct);
    }
    
    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request)
    {
        if (!Roles.All.Contains(request.Role))
        {
            ModelState.AddModelError(nameof(request.Role), $"Role must be one of: {string.Join(", ", Roles.All)}");
            
            return ValidationProblem(ModelState);
        }

        var user = new AppUser { UserName = request.Email, Email = request.Email, EmailConfirmed = true, FullName = request.FullName };
        var created = await userManager.CreateAsync(user, request.Password);
        
        if (!created.Succeeded)
        {
            return IdentityProblem(created);
        }

        var roleAdded = await userManager.AddToRoleAsync(user, request.Role);
        
        if (!roleAdded.Succeeded)
        {
            await userManager.DeleteAsync(user);
            
            return IdentityProblem(roleAdded);
        }

        return StatusCode(StatusCodes.Status201Created, TokenService.ToUserDto(user, request.Role));
    }

    private ActionResult IdentityProblem(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }

        return ValidationProblem(ModelState);
    }
}