using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Admin.Cases;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly ICaseService _cases;
    public IndexModel(ICaseService cases) => _cases = cases;

    public IReadOnlyList<CaseListItem> Cases { get; private set; } = Array.Empty<CaseListItem>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Cases = await _cases.ListAllForAdminAsync(ct);
    }
}
