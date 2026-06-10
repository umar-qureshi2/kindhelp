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

        // Seed roles (always safe — purely structural).
        foreach (var role in Roles.All)
        {
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));
        }

        // Admin seed only when both email AND password are configured. We refuse to seed
        // with empty or placeholder values so the committed appsettings.json can never
        // create a known-default admin in production. Local devs supply these via
        // appsettings.Development.json (written by setup.ps1) or env vars.
        var adminEmail = (config["KindHelp:DefaultAdminEmail"] ?? string.Empty).Trim();
        var adminPassword = config["KindHelp:DefaultAdminPassword"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Default admin not seeded: KindHelp:DefaultAdminEmail and KindHelp:DefaultAdminPassword are not set. " +
                "Set them via environment variables (KindHelp__DefaultAdminEmail / KindHelp__DefaultAdminPassword) " +
                "or appsettings.Development.json. The first admin must be created via the registration flow + manual " +
                "role assignment, or by re-running with the env vars set.");
            return;
        }

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
