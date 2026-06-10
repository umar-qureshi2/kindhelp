using KindHelp.Web.Data;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin.Contributions;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private const int PageSize = 50;
    private readonly ApplicationDbContext _db;
    private readonly IContributionService _contributions;

    public IndexModel(ApplicationDbContext db, IContributionService contributions)
    {
        _db = db;
        _contributions = contributions;
    }

    public PagedResult<Contribution> Rows { get; private set; } =
        new PagedResult<Contribution>(Array.Empty<Contribution>(), 0, 1, PageSize);
    public IReadOnlyList<Case> AllCases { get; private set; } = Array.Empty<Case>();
    public decimal PageTotal { get; private set; }

    [BindProperty(SupportsGet = true)] public int? CaseId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken ct)
    {
        AllCases = await _db.Cases.AsNoTracking().OrderBy(c => c.Title).ToListAsync(ct);

        var q = _db.Contributions
            .AsNoTracking()
            .Include(x => x.Case)
            .Include(x => x.Donor)
            .AsQueryable();
        if (CaseId.HasValue) q = q.Where(x => x.CaseId == CaseId.Value);

        var total = await q.CountAsync(ct);
        if (PageIndex < 1) PageIndex = 1;

        var rows = await q
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        PageTotal = rows.Sum(r => r.Amount);
        Rows = new PagedResult<Contribution>(rows, total, PageIndex, PageSize);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int? caseId, CancellationToken ct)
    {
        await _contributions.DeleteAsync(id, ct);
        TempData["StatusMessage"] = "Contribution deleted; amount credited back to donor wallet.";
        return RedirectToPage(new { caseId });
    }
}
