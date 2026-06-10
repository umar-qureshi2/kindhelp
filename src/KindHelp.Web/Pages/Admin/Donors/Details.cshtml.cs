using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Admin.Donors;

[Authorize(Policy = "AdminOnly")]
public class DetailsModel : PageModel
{
    private readonly IDonorService _donors;
    private readonly IWalletService _wallet;

    public DetailsModel(IDonorService donors, IWalletService wallet)
    {
        _donors = donors;
        _wallet = wallet;
    }

    public Donor? Donor { get; private set; }

    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
    public PagedResult<WalletHistoryRow> History { get; private set; } =
        new PagedResult<WalletHistoryRow>(Array.Empty<WalletHistoryRow>(), 0, 1, 50);

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Donor = await _donors.GetByIdAsync(id, ct);
        if (Donor is null) return NotFound();
        History = await _wallet.GetHistoryPagedAsync(id, PageIndex, 50, ct);
        return Page();
    }
}
