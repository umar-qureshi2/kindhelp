using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Services;

public class WalletService : IWalletService
{
    private const int MaxConcurrencyRetries = 4;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<WalletService> _logger;

    public WalletService(ApplicationDbContext db, ILogger<WalletService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ---------- Public API ----------

    public Task<WalletTransaction> DepositAsync(
        int donorId, decimal amount, ContributionMethod method, DateTime occurredAtUtc,
        string? reference, string? notes, string recordedByUserId, CancellationToken ct = default)
        => WithConcurrencyRetryAsync(
            () => DepositCoreAsync(donorId, amount, method, occurredAtUtc, reference, notes, recordedByUserId, ct),
            "Deposit", ct);

    public Task<Contribution> AllocateAsync(
        int donorId, int caseId, decimal amount, DateTime receivedAtUtc,
        string? reference, string recordedByUserId, CancellationToken ct = default)
        => WithConcurrencyRetryAsync(
            () => AllocateCoreAsync(donorId, caseId, amount, receivedAtUtc, reference, recordedByUserId, ct),
            "Allocate", ct);

    public Task<WalletTransaction> AdjustAsync(
        int donorId, decimal amount, string reason, string recordedByUserId, CancellationToken ct = default)
        => WithConcurrencyRetryAsync(
            () => AdjustCoreAsync(donorId, amount, reason, recordedByUserId, ct),
            "Adjust", ct);

    public Task<WalletTransaction> CorrectAmountAsync(
        int originalTransactionId, decimal correctedAmount, string reason, string recordedByUserId, CancellationToken ct = default)
        => WithConcurrencyRetryAsync(
            () => CorrectAmountCoreAsync(originalTransactionId, correctedAmount, reason, recordedByUserId, ct),
            "Correct", ct);

    public async Task<IReadOnlyList<WalletHistoryRow>> GetHistoryAsync(int donorId, CancellationToken ct = default)
    {
        return await _db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.DonorId == donorId)
            .OrderByDescending(t => t.OccurredAtUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => new WalletHistoryRow(
                t.Id,
                t.Type,
                t.Amount,
                t.BalanceAfter,
                t.OccurredAtUtc,
                t.Reference,
                t.Method,
                t.CaseId,
                t.Case != null ? t.Case.Title : null,
                t.Case != null ? t.Case.Slug  : null,
                t.CorrectsWalletTransactionId,
                t.Notes))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<WalletHistoryRow>> GetHistoryPagedAsync(int donorId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 50;

        var q = _db.WalletTransactions.AsNoTracking().Where(t => t.DonorId == donorId);
        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(t => t.OccurredAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new WalletHistoryRow(
                t.Id, t.Type, t.Amount, t.BalanceAfter, t.OccurredAtUtc,
                t.Reference, t.Method, t.CaseId,
                t.Case != null ? t.Case.Title : null,
                t.Case != null ? t.Case.Slug  : null,
                t.CorrectsWalletTransactionId,
                t.Notes))
            .ToListAsync(ct);

        return new PagedResult<WalletHistoryRow>(rows, total, pageIndex, pageSize);
    }

    // ---------- Core operations ----------

    private async Task<WalletTransaction> DepositCoreAsync(
        int donorId, decimal amount, ContributionMethod method, DateTime occurredAtUtc,
        string? reference, string? notes, string recordedByUserId, CancellationToken ct)
    {
        if (amount <= 0) throw new InvalidOperationException("Deposit amount must be positive.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var donor = await _db.Donors.FirstOrDefaultAsync(d => d.Id == donorId, ct)
            ?? throw new InvalidOperationException("Donor not found.");

        donor.WalletBalance += amount;

        var entry = new WalletTransaction
        {
            DonorId = donor.Id,
            Type = WalletTransactionType.Deposit,
            Amount = amount,
            BalanceAfter = donor.WalletBalance,
            OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc == default ? DateTime.UtcNow : occurredAtUtc, DateTimeKind.Utc),
            Method = method,
            Reference = reference,
            Notes = notes,
            RecordedByUserId = recordedByUserId
        };
        _db.WalletTransactions.Add(entry);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return entry;
    }

    private async Task<Contribution> AllocateCoreAsync(
        int donorId, int caseId, decimal amount, DateTime receivedAtUtc,
        string? reference, string recordedByUserId, CancellationToken ct)
    {
        if (amount <= 0) throw new InvalidOperationException("Allocation amount must be positive.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var donor = await _db.Donors.FirstOrDefaultAsync(d => d.Id == donorId, ct)
            ?? throw new InvalidOperationException("Donor not found.");

        if (donor.WalletBalance < amount)
            throw new InsufficientWalletBalanceException(donor.WalletBalance, amount);

        var caseExists = await _db.Cases.AnyAsync(c => c.Id == caseId, ct);
        if (!caseExists) throw new InvalidOperationException("Case not found.");

        donor.WalletBalance -= amount;

        var contribution = new Contribution
        {
            CaseId = caseId,
            DonorId = donor.Id,
            Amount = amount,
            ReceivedAtUtc = DateTime.SpecifyKind(receivedAtUtc == default ? DateTime.UtcNow : receivedAtUtc, DateTimeKind.Utc),
            Reference = reference,
            RecordedByUserId = recordedByUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Contributions.Add(contribution);
        await _db.SaveChangesAsync(ct);

        var entry = new WalletTransaction
        {
            DonorId = donor.Id,
            Type = WalletTransactionType.Allocation,
            Amount = -amount,
            BalanceAfter = donor.WalletBalance,
            OccurredAtUtc = contribution.ReceivedAtUtc,
            CaseId = caseId,
            ContributionId = contribution.Id,
            Reference = reference,
            RecordedByUserId = recordedByUserId
        };
        _db.WalletTransactions.Add(entry);
        await _db.SaveChangesAsync(ct);

        contribution.WalletTransactionId = entry.Id;
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return contribution;
    }

    private async Task<WalletTransaction> AdjustCoreAsync(
        int donorId, decimal amount, string reason, string recordedByUserId, CancellationToken ct)
    {
        if (amount == 0) throw new InvalidOperationException("Adjustment amount must be non-zero.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var donor = await _db.Donors.FirstOrDefaultAsync(d => d.Id == donorId, ct)
            ?? throw new InvalidOperationException("Donor not found.");

        var newBalance = donor.WalletBalance + amount;
        if (newBalance < 0)
            throw new InsufficientWalletBalanceException(donor.WalletBalance, -amount);

        donor.WalletBalance = newBalance;

        var entry = new WalletTransaction
        {
            DonorId = donor.Id,
            Type = WalletTransactionType.Adjustment,
            Amount = amount,
            BalanceAfter = newBalance,
            OccurredAtUtc = DateTime.UtcNow,
            Notes = reason,
            RecordedByUserId = recordedByUserId
        };
        _db.WalletTransactions.Add(entry);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return entry;
    }

    private async Task<WalletTransaction> CorrectAmountCoreAsync(
        int originalTransactionId, decimal correctedAmount, string reason, string recordedByUserId, CancellationToken ct)
    {
        if (correctedAmount <= 0)
            throw new InvalidOperationException("Corrected amount must be positive.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required when correcting a wallet transaction.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var original = await _db.WalletTransactions
            .Include(t => t.Contribution)
            .FirstOrDefaultAsync(t => t.Id == originalTransactionId, ct)
            ?? throw new InvalidOperationException("Original transaction not found.");

        if (original.Type != WalletTransactionType.Deposit && original.Type != WalletTransactionType.Allocation)
            throw new InvalidOperationException("Only deposits and allocations can be corrected. Use Adjust for other changes.");

        // Don't allow correcting a correction (avoids confusing chains).
        if (original.CorrectsWalletTransactionId is not null)
            throw new InvalidOperationException("This entry is itself a correction. Correct the underlying original instead.");

        var donor = await _db.Donors.FirstOrDefaultAsync(d => d.Id == original.DonorId, ct)
            ?? throw new InvalidOperationException("Donor not found.");

        // Wallet impact of the corrected entry would be:
        //   Deposit:    +correctedAmount  (credit)
        //   Allocation: -correctedAmount  (debit)
        var correctedSignedImpact = original.Type == WalletTransactionType.Deposit
            ? correctedAmount
            : -correctedAmount;

        var delta = correctedSignedImpact - original.Amount;
        if (delta == 0)
            throw new InvalidOperationException("Corrected amount equals the original — nothing to change.");

        var newBalance = donor.WalletBalance + delta;
        if (newBalance < 0)
            throw new InsufficientWalletBalanceException(donor.WalletBalance, -delta);

        donor.WalletBalance = newBalance;

        var correction = new WalletTransaction
        {
            DonorId = donor.Id,
            Type = WalletTransactionType.Adjustment,
            Amount = delta,
            BalanceAfter = newBalance,
            OccurredAtUtc = DateTime.UtcNow,
            CaseId = original.CaseId,
            CorrectsWalletTransactionId = original.Id,
            Notes = $"Correction of transaction #{original.Id} ({original.Type}): {reason.Trim()}",
            RecordedByUserId = recordedByUserId
        };
        _db.WalletTransactions.Add(correction);

        // If the original was an Allocation, sync the linked Contribution.Amount so that
        // the case's "raised" total reflects the corrected figure.
        if (original.Type == WalletTransactionType.Allocation && original.Contribution is not null)
        {
            original.Contribution.Amount = correctedAmount;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Wallet transaction #{Original} corrected by {Delta:N2} (new balance {Balance:N2}); reason: {Reason}",
            original.Id, delta, newBalance, reason);
        return correction;
    }

    // ---------- Concurrency retry ----------

    /// <summary>
    /// Wraps a wallet write so that an optimistic-concurrency conflict on <c>Donor.xmin</c>
    /// causes us to clear the change tracker, back off briefly, and retry. After
    /// <see cref="MaxConcurrencyRetries"/> failures we surface a clear error rather than
    /// risking divergence between Donor.WalletBalance and the WalletTransactions ledger.
    /// </summary>
    private async Task<T> WithConcurrencyRetryAsync<T>(Func<Task<T>> op, string operationName, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                return await op();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                _logger.LogWarning("{Op} concurrency conflict on attempt {Attempt}; retrying.", operationName, attempt);
                _db.ChangeTracker.Clear();
                await Task.Delay(40 * attempt, ct);
            }
        }
        throw new InvalidOperationException(
            $"{operationName} failed after {MaxConcurrencyRetries} attempts due to concurrent wallet updates. Please retry.");
    }
}
