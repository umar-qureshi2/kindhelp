using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Cases;

[AllowAnonymous]
public class DetailsModel : PageModel
{
    private readonly ICaseService _cases;
    public DetailsModel(ICaseService cases) => _cases = cases;

    public CaseDetailView? View { get; private set; }

    public async Task OnGetAsync(string slug, CancellationToken ct)
    {
        View = await _cases.GetPublicBySlugAsync(slug, ct);
    }
}
