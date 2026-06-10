using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.My;

[Authorize]
public class DashboardModel : MyPageBase
{
    private readonly IContributionService _contributions;

    public DashboardModel(
        UserManager<ApplicationUser> userManager,
        IDonorService donors,
        IContributionService contributions)
        : base(userManager, donors)
    {
        _contributions = contributions;
    }

    public IReadOnlyList<DonorCaseSummary> Summaries { get; private set; } = Array.Empty<DonorCaseSummary>();
    public IReadOnlyList<DonorUpdateRow> Updates { get; private set; } = Array.Empty<DonorUpdateRow>();
    public decimal TotalContributed { get; private set; }
    public decimal WalletBalance { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var donor = await ResolveCurrentDonorAsync(ct);
        if (donor is null) return Challenge();

        Summaries = await _contributions.GetMyCaseSummariesAsync(donor.Id, ct);
        Updates = await _contributions.GetMyCaseUpdatesAsync(donor.Id, max: 10, ct);
        TotalContributed = Summaries.Sum(s => s.MyTotal);
        WalletBalance = donor.WalletBalance;
        return Page();
    }
}
