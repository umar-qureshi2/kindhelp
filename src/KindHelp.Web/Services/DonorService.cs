using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Services;

public class DonorService : IDonorService
{
    private readonly ApplicationDbContext _db;
    public DonorService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DonorListItem>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var result = await ListPagedAsync(search, 1, 500, ct);
        return result.Items;
    }

    public async Task<PagedResult<DonorListItem>> ListPagedAsync(string? search, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 50;

        var q = _db.Donors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(d =>
                (d.DisplayName != null && d.DisplayName.ToLower().Contains(s)) ||
                (d.Email       != null && d.Email.ToLower().Contains(s)) ||
                (d.PhoneNumber != null && d.PhoneNumber.Contains(s)));
        }

        var total = await q.CountAsync(ct);

        // Fetch the page's donor rows without the contribution count subquery,
        // then batch-load the contribution counts in a single GROUP BY query
        // and merge in memory. This avoids the N-correlated-subqueries pattern.
        var page = await q
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.DisplayName,
                d.Email,
                d.PhoneNumber,
                d.ApplicationUserId,
                d.WalletBalance,
                d.CreatedAtUtc
            })
            .ToListAsync(ct);

        if (page.Count == 0)
            return new PagedResult<DonorListItem>(Array.Empty<DonorListItem>(), total, pageIndex, pageSize);

        var ids = page.Select(p => p.Id).ToArray();
        var counts = await _db.Contributions
            .AsNoTracking()
            .Where(c => ids.Contains(c.DonorId))
            .GroupBy(c => c.DonorId)
            .Select(g => new { DonorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonorId, x => x.Count, ct);

        var items = page
            .Select(d => new DonorListItem(
                d.Id,
                d.DisplayName ?? d.Email ?? d.PhoneNumber ?? ("Donor #" + d.Id),
                d.Email,
                d.PhoneNumber,
                d.ApplicationUserId != null,
                d.WalletBalance,
                counts.TryGetValue(d.Id, out var c) ? c : 0,
                d.CreatedAtUtc))
            .ToList();

        return new PagedResult<DonorListItem>(items, total, pageIndex, pageSize);
    }

    public Task<Donor?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Donors.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Donor?> FindByContactAsync(string? email, string? phone, CancellationToken ct = default)
    {
        var em = NormalizeEmail(email);
        var ph = NormalizePhone(phone);
        if (em is null && ph is null) return null;

        return await _db.Donors.FirstOrDefaultAsync(d =>
            (em != null && d.Email != null && d.Email.ToLower() == em) ||
            (ph != null && d.PhoneNumber == ph), ct);
    }

    public async Task<Donor> EnsureForUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var existing = await _db.Donors.FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id, ct);
        if (existing is not null) return existing;

        // Try to claim any admin-created donor whose email matches this account.
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var em = user.Email!.Trim().ToLower();
            var orphan = await _db.Donors.FirstOrDefaultAsync(d =>
                d.ApplicationUserId == null && d.Email != null && d.Email.ToLower() == em, ct);
            if (orphan is not null)
            {
                orphan.ApplicationUserId = user.Id;
                if (string.IsNullOrWhiteSpace(orphan.DisplayName) && !string.IsNullOrWhiteSpace(user.DisplayName))
                    orphan.DisplayName = user.DisplayName;
                await _db.SaveChangesAsync(ct);
                return orphan;
            }
        }

        var donor = new Donor
        {
            ApplicationUserId = user.Id,
            DisplayName = user.DisplayName,
            Email = NormalizeEmail(user.Email),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Donors.Add(donor);
        await _db.SaveChangesAsync(ct);
        return donor;
    }

    public async Task<Donor> CreateAdminManagedAsync(string? displayName, string? email, string? phone, string? notes, string createdByUserId, CancellationToken ct = default)
    {
        var em = NormalizeEmail(email);
        var ph = NormalizePhone(phone);

        if (em is null && ph is null)
            throw new InvalidOperationException("Either an email or a phone number is required to create a donor.");

        // De-dupe: if a donor with the same email or phone already exists, return that one.
        var existing = await FindByContactAsync(em, ph, ct);
        if (existing is not null) return existing;

        var donor = new Donor
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName!.Trim(),
            Email = em,
            PhoneNumber = ph,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };
        _db.Donors.Add(donor);
        await _db.SaveChangesAsync(ct);
        return donor;
    }

    public async Task UpdateAsync(int id, string? displayName, string? email, string? phone, string? notes, CancellationToken ct = default)
    {
        var donor = await _db.Donors.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new InvalidOperationException("Donor not found.");

        donor.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName!.Trim();
        donor.Email = NormalizeEmail(email);
        donor.PhoneNumber = NormalizePhone(phone);
        donor.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        await _db.SaveChangesAsync(ct);
    }

    private static string? NormalizeEmail(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLower();

    private static string? NormalizePhone(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // Keep + and digits only.
        var cleaned = new string(s.Where(c => c == '+' || char.IsDigit(c)).ToArray());
        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }
}
