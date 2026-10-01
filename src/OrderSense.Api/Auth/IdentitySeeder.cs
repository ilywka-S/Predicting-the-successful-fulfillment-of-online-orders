using Microsoft.AspNetCore.Identity;
using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Auth;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
        
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Administrator" };
        var result = await userManager.CreateAsync(admin, password);
        
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Cannot create admin user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        
        logger.LogInformation("Admin user {Email} created", email);
    }
}