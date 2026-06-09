using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public int TotalCases { get; private set; }
    public int ActiveCases { get; private set; }
    public decimal TotalRaised { get; private set; }
    public int DonorCount { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        TotalCases = await _db.Cases.CountAsync(ct);
        ActiveCases = await _db.Cases.CountAsync(c => c.Status == CaseStatus.Active, ct);
        TotalRaised = await _db.Contributions.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        DonorCount = await _db.Contributions.Select(x => x.DonorUserId).Distinct().CountAsync(ct);
    }
}
