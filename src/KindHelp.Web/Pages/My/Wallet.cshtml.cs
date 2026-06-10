using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.My;

[Authorize]
public class WalletModel : MyPageBase
{
    private readonly IWalletService _wallet;

    public WalletModel(
        UserManager<ApplicationUser> userManager,
        IDonorService donors,
        IWalletService wallet)
        : base(userManager, donors)
    {
        _wallet = wallet;
    }

    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
    public decimal Balance { get; private set; }
    public PagedResult<WalletHistoryRow> History { get; private set; } =
        new PagedResult<WalletHistoryRow>(Array.Empty<WalletHistoryRow>(), 0, 1, 50);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var donor = await ResolveCurrentDonorAsync(ct);
        if (donor is null) return Challenge();

        Balance = donor.WalletBalance;
        History = await _wallet.GetHistoryPagedAsync(donor.Id, PageIndex, 50, ct);
        return Page();
    }
}
