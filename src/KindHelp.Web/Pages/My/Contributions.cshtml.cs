using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.My;

[Authorize]
public class ContributionsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IContributionService _contributions;

    public ContributionsModel(UserManager<ApplicationUser> userManager, IContributionService contributions)
    {
        _userManager = userManager;
        _contributions = contributions;
    }

    public IReadOnlyList<DonorContributionRow> Rows { get; private set; } = Array.Empty<DonorContributionRow>();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        // PRIVACY: strict per-user filter; never accept a userId from the request.
        Rows = await _contributions.GetMyContributionsAsync(userId, ct);
        return Page();
    }
}
