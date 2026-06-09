using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages;

[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly ICaseService _cases;

    public IndexModel(ICaseService cases) => _cases = cases;

    public IReadOnlyList<CaseListItem> Featured { get; private set; } = Array.Empty<CaseListItem>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var all = await _cases.ListPublicAsync(ct);
        Featured = all.Take(6).ToList();
    }
}
