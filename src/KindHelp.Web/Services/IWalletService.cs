using KindHelp.Web.Models;

namespace KindHelp.Web.Services;

/// <summary>Thrown by <c>WalletService.AllocateAsync</c> when the donor's wallet balance is below the requested amount.</summary>
public class InsufficientWalletBalanceException : Exception
{
    public decimal CurrentBalance { get; }
    public decimal Requested { get; }
    public InsufficientWalletBalanceException(decimal current, decimal requested)
        : base($"Donor wallet has {current:N2} but allocation requested {requested:N2}.")
    {
        CurrentBalance = current;
        Requested = requested;
    }
}

public record WalletHistoryRow(
    int Id,
    WalletTransactionType Type,
    decimal Amount,
    decimal BalanceAfter,
    DateTime OccurredAtUtc,
    string? Reference,
    ContributionMethod? Method,
    int? CaseId,
    string? CaseTitle,
    string? CaseSlug,
    int? CorrectsTransactionId,
    string? Notes);

public interface IWalletService
{
    /// <summary>Credit a donor's wallet. Creates a Deposit transaction and increments balance atomically.</summary>
    Task<WalletTransaction> DepositAsync(
        int donorId,
        decimal amount,
        ContributionMethod method,
        DateTime occurredAtUtc,
        string? reference,
        string? notes,
        string recordedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Allocate funds from a donor's wallet to a case. Atomically:
    /// (a) creates the Contribution, (b) debits the wallet via an Allocation WalletTransaction,
    /// (c) links the two. Throws <see cref="InsufficientWalletBalanceException"/> if balance is low.
    /// </summary>
    Task<Contribution> AllocateAsync(
        int donorId,
        int caseId,
        decimal amount,
        DateTime receivedAtUtc,
        string? reference,
        string recordedByUserId,
        CancellationToken ct = default);

    /// <summary>Manual correction. Positive or negative amount allowed; negative cannot exceed current balance.</summary>
    Task<WalletTransaction> AdjustAsync(
        int donorId,
        decimal amount,
        string reason,
        string recordedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Admin-only: fix a typo on a prior Deposit or Allocation. Keeps the original row
    /// immutable for audit and appends a compensating Adjustment carrying the delta.
    /// If the original was an Allocation, also updates the linked Contribution.Amount.
    /// Throws if the resulting wallet balance would go negative.
    /// </summary>
    Task<WalletTransaction> CorrectAmountAsync(
        int originalTransactionId,
        decimal correctedAmount,
        string reason,
        string recordedByUserId,
        CancellationToken ct = default);

    /// <summary>Full wallet history for a donor. Admin or that donor only. Prefer the paged variant for UI.</summary>
    Task<IReadOnlyList<WalletHistoryRow>> GetHistoryAsync(int donorId, CancellationToken ct = default);

    Task<PagedResult<WalletHistoryRow>> GetHistoryPagedAsync(int donorId, int pageIndex, int pageSize, CancellationToken ct = default);
}
