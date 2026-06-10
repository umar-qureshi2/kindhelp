using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Admin.Donors;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IDonorService _donors;
    public IndexModel(IDonorService donors) => _donors = donors;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
    public PagedResult<DonorListItem> Donors { get; private set; } =
        new PagedResult<DonorListItem>(Array.Empty<DonorListItem>(), 0, 1, 50);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Donors = await _donors.ListPagedAsync(Search, PageIndex, 50, ct);
    }
}
