using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.My;

/// <summary>
/// Shared base for every page under <c>/My/*</c>. Resolves the current Donor row from the
/// authenticated <see cref="ApplicationUser"/>, so PageModels don't repeat the
/// <c>GetUserAsync → EnsureForUserAsync</c> dance. PRIVACY: the Donor identity always
/// comes from <c>UserManager.GetUserAsync(User)</c>, never from the request.
/// </summary>
public abstract class MyPageBase : PageModel
{
    protected readonly UserManager<ApplicationUser> UserManager;
    protected readonly IDonorService Donors;

    protected MyPageBase(UserManager<ApplicationUser> userManager, IDonorService donors)
    {
        UserManager = userManager;
        Donors = donors;
    }

    /// <summary>
    /// Returns the Donor row for the signed-in user, creating one if needed. Returns null
    /// when no user is signed in — caller should <c>Challenge()</c>.
    /// </summary>
    protected async Task<Donor?> ResolveCurrentDonorAsync(CancellationToken ct = default)
    {
        var user = await UserManager.GetUserAsync(User);
        if (user is null) return null;
        return await Donors.EnsureForUserAsync(user, ct);
    }
}
