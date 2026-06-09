using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin.Contributions;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IReadOnlyList<Contribution> Rows { get; private set; } = Array.Empty<Contribution>();
    public IReadOnlyList<Case> AllCases { get; private set; } = Array.Empty<Case>();

    [BindProperty(SupportsGet = true)] public int? CaseId { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        AllCases = await _db.Cases
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .ToListAsync(ct);

        var q = _db.Contributions
            .AsNoTracking()
            .Include(x => x.Case)
            .Include(x => x.Donor)
            .AsQueryable();

        if (CaseId.HasValue) q = q.Where(x => x.CaseId == CaseId.Value);

        Rows = await q.OrderByDescending(x => x.ReceivedAtUtc).ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int? caseId, CancellationToken ct)
    {
        var entity = await _db.Contributions.FindAsync(new object?[] { id }, ct);
        if (entity is not null)
        {
            _db.Contributions.Remove(entity);
            await _db.SaveChangesAsync(ct);
            TempData["StatusMessage"] = "Contribution deleted.";
        }
        return RedirectToPage(new { caseId });
    }
}
