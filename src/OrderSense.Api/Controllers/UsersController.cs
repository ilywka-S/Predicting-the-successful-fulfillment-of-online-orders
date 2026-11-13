using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!Roles.All.Contains(request.Role))
        {
            ModelState.AddModelError(nameof(request.Role), $"Role must be one of: {string.Join(", ", Roles.All)}");

            return ValidationProblem(ModelState);
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var user = new AppUser { UserName = request.Email, Email = request.Email, EmailConfirmed = true, FullName = request.FullName };
            var result = await CreateUserAsync(user, request.Password);

            if (result.Succeeded)
            {
                result = await userManager.AddToRoleAsync(user, request.Role);
            }

            if (!result.Succeeded)
            {
                return IdentityProblem(result);
            }

            await transaction.CommitAsync(ct);

            return StatusCode(StatusCodes.Status201Created, TokenService.ToUserDto(user, request.Role));
        });
    }

    private async Task<IdentityResult> CreateUserAsync(AppUser user, string password)
    {
        try
        {
            return await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return IdentityResult.Failed(userManager.ErrorDescriber.DuplicateEmail(user.Email!));
        }
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