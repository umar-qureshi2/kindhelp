using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Services;

public class CaseService : ICaseService
{
    private readonly ApplicationDbContext _db;

    public CaseService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CaseListItem>> ListPublicAsync(CancellationToken ct = default)
    {
        // Public list shows only Active or Funded cases — never drafts or archived ones.
        var visibleStatuses = new[] { CaseStatus.Active, CaseStatus.Funded };

        return await _db.Cases
            .AsNoTracking()
            .Where(c => visibleStatuses.Contains(c.Status))
            .OrderByDescending(c => c.PublishedAtUtc ?? c.CreatedAtUtc)
            .Select(c => new CaseListItem(
                c.Id,
                c.Slug,
                c.Title,
                c.Summary,
                c.Status,
                c.Category,
                c.Photos.Where(p => p.IsCover).Select(p => p.RelativePath).FirstOrDefault()
                    ?? c.Photos.OrderBy(p => p.SortOrder).Select(p => p.RelativePath).FirstOrDefault(),
                c.GoalAmount,
                c.Contributions.Sum(x => (decimal?)x.Amount) ?? 0m,
                c.Contributions.Select(x => x.DonorUserId).Distinct().Count()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CaseListItem>> ListAllForAdminAsync(CancellationToken ct = default)
    {
        return await _db.Cases
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new CaseListItem(
                c.Id,
                c.Slug,
                c.Title,
                c.Summary,
                c.Status,
                c.Category,
                c.Photos.Where(p => p.IsCover).Select(p => p.RelativePath).FirstOrDefault()
                    ?? c.Photos.OrderBy(p => p.SortOrder).Select(p => p.RelativePath).FirstOrDefault(),
                c.GoalAmount,
                c.Contributions.Sum(x => (decimal?)x.Amount) ?? 0m,
                c.Contributions.Select(x => x.DonorUserId).Distinct().Count()))
            .ToListAsync(ct);
    }

    public async Task<CaseDetailView?> GetPublicBySlugAsync(string slug, CancellationToken ct = default)
    {
        var visibleStatuses = new[] { CaseStatus.Active, CaseStatus.Funded };

        var c = await _db.Cases
            .AsNoTracking()
            .Include(x => x.Photos.OrderBy(p => p.SortOrder))
            .Include(x => x.Updates.OrderByDescending(u => u.PostedAtUtc))
            .Where(x => x.Slug == slug && visibleStatuses.Contains(x.Status))
            .FirstOrDefaultAsync(ct);

        if (c is null) return null;

        // PRIVACY: aggregate only — sum + distinct donor count.
        var raised = await _db.Contributions
            .Where(x => x.CaseId == c.Id)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        var donorCount = await _db.Contributions
            .Where(x => x.CaseId == c.Id)
            .Select(x => x.DonorUserId)
            .Distinct()
            .CountAsync(ct);

        return new CaseDetailView(
            c,
            c.Photos.ToList(),
            c.Updates.ToList(),
            raised,
            donorCount);
    }

    public async Task<Case?> GetForAdminAsync(int id, CancellationToken ct = default)
    {
        return await _db.Cases
            .Include(x => x.Photos)
            .Include(x => x.Updates.OrderByDescending(u => u.PostedAtUtc))
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }
}
