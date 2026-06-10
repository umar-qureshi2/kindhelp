using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Services;

public class ContributionService : IContributionService
{
    private readonly ApplicationDbContext _db;

    public ContributionService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DonorCaseSummary>> GetMyCaseSummariesAsync(int donorId, CancellationToken ct = default)
    {
        if (donorId <= 0) return Array.Empty<DonorCaseSummary>();

        // PRIVACY: filter strictly by donorId. We never join to other donors' rows.
        var aggregates = await _db.Contributions
            .AsNoTracking()
            .Where(x => x.DonorId == donorId)
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

    public async Task<IReadOnlyList<DonorContributionRow>> GetMyContributionsAsync(int donorId, CancellationToken ct = default)
    {
        if (donorId <= 0) return Array.Empty<DonorContributionRow>();

        return await _db.Contributions
            .AsNoTracking()
            .Where(x => x.DonorId == donorId)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Select(x => new DonorContributionRow(
                x.Id,
                x.CaseId,
                x.Case.Slug,
                x.Case.Title,
                x.Amount,
                x.ReceivedAtUtc,
                x.Reference))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<DonorContributionRow>> GetMyContributionsPagedAsync(int donorId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        if (donorId <= 0) return new PagedResult<DonorContributionRow>(Array.Empty<DonorContributionRow>(), 0, 1, pageSize);
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 50;

        var q = _db.Contributions.AsNoTracking().Where(x => x.DonorId == donorId);
        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DonorContributionRow(
                x.Id, x.CaseId, x.Case.Slug, x.Case.Title,
                x.Amount, x.ReceivedAtUtc, x.Reference))
            .ToListAsync(ct);

        return new PagedResult<DonorContributionRow>(rows, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<DonorUpdateRow>> GetMyCaseUpdatesAsync(int donorId, int max = 30, CancellationToken ct = default)
    {
        if (donorId <= 0) return Array.Empty<DonorUpdateRow>();

        var supportedCaseIds = _db.Contributions
            .Where(x => x.DonorId == donorId)
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

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Contributions
            .Include(c => c.WalletTransaction)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return;

        // Reverse the wallet movement: credit the donor's wallet back by the amount,
        // and write a compensating Adjustment so the audit trail stays intact.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var donor = await _db.Donors.FirstOrDefaultAsync(d => d.Id == entity.DonorId, ct);
        if (donor is not null)
        {
            donor.WalletBalance += entity.Amount;
            _db.WalletTransactions.Add(new WalletTransaction
            {
                DonorId = donor.Id,
                Type = WalletTransactionType.Adjustment,
                Amount = entity.Amount,
                BalanceAfter = donor.WalletBalance,
                OccurredAtUtc = DateTime.UtcNow,
                Notes = $"Reverted contribution #{entity.Id} to case {entity.CaseId}",
                CaseId = entity.CaseId
            });
        }

        _db.Contributions.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
