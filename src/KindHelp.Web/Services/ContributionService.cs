using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Services;

public class ContributionService : IContributionService
{
    private readonly ApplicationDbContext _db;

    public ContributionService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DonorCaseSummary>> GetMyCaseSummariesAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Array.Empty<DonorCaseSummary>();

        // PRIVACY: filter strictly by userId. We never join to other donors' rows.
        var aggregates = await _db.Contributions
            .AsNoTracking()
            .Where(x => x.DonorUserId == userId)
            .GroupBy(x => x.CaseId)
            .Select(g => new
            {
                CaseId = g.Key,
                MyTotal = g.Sum(x => x.Amount),
                Count = g.Count(),
                Last = g.Max(x => x.ReceivedAtUtc)
            })
            .ToListAsync(ct);

        if (aggregates.Count == 0) return Array.Empty<DonorCaseSummary>();

        var caseIds = aggregates.Select(a => a.CaseId).ToArray();
        var cases = await _db.Cases
            .AsNoTracking()
            .Where(c => caseIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.Slug,
                c.Title,
                c.Status,
                Cover = c.Photos.Where(p => p.IsCover).Select(p => p.RelativePath).FirstOrDefault()
                        ?? c.Photos.OrderBy(p => p.SortOrder).Select(p => p.RelativePath).FirstOrDefault()
            })
            .ToListAsync(ct);

        return aggregates
            .Join(cases, a => a.CaseId, c => c.Id, (a, c) => new DonorCaseSummary(
                c.Id, c.Slug, c.Title, c.Status, c.Cover,
                a.MyTotal, a.Count, a.Last))
            .OrderByDescending(x => x.LastContributionUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<DonorContributionRow>> GetMyContributionsAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Array.Empty<DonorContributionRow>();

        return await _db.Contributions
            .AsNoTracking()
            .Where(x => x.DonorUserId == userId)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Select(x => new DonorContributionRow(
                x.Id,
                x.CaseId,
                x.Case.Slug,
                x.Case.Title,
                x.Amount,
                x.Method,
                x.ReceivedAtUtc,
                x.Reference))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DonorUpdateRow>> GetMyCaseUpdatesAsync(string userId, int max = 30, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Array.Empty<DonorUpdateRow>();

        // Updates from cases this donor supported. Strictly filtered via the contributions subquery.
        var supportedCaseIds = _db.Contributions
            .Where(x => x.DonorUserId == userId)
            .Select(x => x.CaseId)
            .Distinct();

        return await _db.CaseUpdates
            .AsNoTracking()
            .Where(u => supportedCaseIds.Contains(u.CaseId))
            .OrderByDescending(u => u.PostedAtUtc)
            .Take(max)
            .Select(u => new DonorUpdateRow(
                u.CaseId,
                u.Case.Slug,
                u.Case.Title,
                u.Id,
                u.Title,
                u.Body,
                u.PostedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Contribution>> GetForCaseAdminAsync(int caseId, CancellationToken ct = default)
    {
        return await _db.Contributions
            .AsNoTracking()
            .Include(x => x.Donor)
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<Contribution> RecordAsync(Contribution input, string recordedByUserId, CancellationToken ct = default)
    {
        input.RecordedByUserId = recordedByUserId;
        input.CreatedAtUtc = DateTime.UtcNow;
        if (input.ReceivedAtUtc == default) input.ReceivedAtUtc = DateTime.UtcNow;

        _db.Contributions.Add(input);
        await _db.SaveChangesAsync(ct);
        return input;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Contributions.FindAsync(new object?[] { id }, ct);
        if (entity is null) return;
        _db.Contributions.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
