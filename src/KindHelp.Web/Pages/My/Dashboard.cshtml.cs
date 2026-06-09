using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.My;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IContributionService _contributions;

    public DashboardModel(UserManager<ApplicationUser> userManager, IContributionService contributions)
    {
        _userManager = userManager;
        _contributions = contributions;
    }

    public IReadOnlyList<DonorCaseSummary> Summaries { get; private set; } = Array.Empty<DonorCaseSummary>();
    public IReadOnlyList<DonorUpdateRow> Updates { get; private set; } = Array.Empty<DonorUpdateRow>();
    public decimal TotalContributed { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // PRIVACY: always derive userId from the authenticated principal.
        // Never accept it from a query string or form post.
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        Summaries = await _contributions.GetMyCaseSummariesAsync(userId, ct);
        Updates = await _contributions.GetMyCaseUpdatesAsync(userId, max: 10, ct);
        TotalContributed = Summaries.Sum(s => s.MyTotal);
        return Page();
    }
}
