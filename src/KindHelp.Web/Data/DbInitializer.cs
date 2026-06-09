using KindHelp.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace KindHelp.Web.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Seed roles
        foreach (var role in Roles.All)
        {
            if (!await roleMgr.RoleExistsAsync(role))
            {
                await roleMgr.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed default admin if none exists
        var adminEmail = config["KindHelp:DefaultAdminEmail"] ?? "admin@kindhelp.local";
        var adminPassword = config["KindHelp:DefaultAdminPassword"] ?? "ChangeMe!2026";

        var admin = await userMgr.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "Site Admin"
            };
            var result = await userMgr.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userMgr.AddToRoleAsync(admin, Roles.Admin);
                logger.LogInformation("Seeded default admin user: {Email}", adminEmail);
            }
            else
            {
                logger.LogWarning("Could not seed default admin: {Errors}",
                    string.Join("; ", result.Errors.Select(x => x.Description)));
            }
        }
        else if (!await userMgr.IsInRoleAsync(admin, Roles.Admin))
        {
            await userMgr.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}
