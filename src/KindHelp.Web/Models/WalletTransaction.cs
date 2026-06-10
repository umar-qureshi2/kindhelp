using System.ComponentModel.DataAnnotations;

namespace KindHelp.Web.Models;

public enum WalletTransactionType
{
    /// <summary>Money received from the donor into their wallet. Amount &gt; 0.</summary>
    Deposit = 0,

    /// <summary>Money allocated from the wallet out to a case. Amount &lt; 0; CaseId required.</summary>
    Allocation = 1,

    /// <summary>Money returned to the donor (e.g. unused funds). Amount &lt; 0.</summary>
    Refund = 2,

    /// <summary>Manual correction (positive or negative). Use sparingly; admin only.</summary>
    Adjustment = 3
}

/// <summary>
/// Append-only audit log of every change to a donor's wallet. Each row records the signed
/// amount and a snapshot of the resulting balance for fast point-in-time reconstruction.
/// The donor's <see cref="Donor.WalletBalance"/> field is a denormalised cache of SUM(Amount)
/// across these rows; both are written atomically inside <c>WalletService</c>.
/// </summary>
public class WalletTransaction
{
    public int Id { get; set; }

    public int DonorId { get; set; }
    public Donor Donor { get; set; } = null!;

    public WalletTransactionType Type { get; set; }

    /// <summary>Signed amount. Positive for credits (Deposit, positive Adjustment), negative for debits.</summary>
    public decimal Amount { get; set; }

    /// <summary>Wallet balance immediately after this transaction. Snapshot for audit and faster history queries.</summary>
    public decimal BalanceAfter { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Free-form admin reference (bank ref, cheque number, etc.).</summary>
    [StringLength(400)]
    public string? Reference { get; set; }

    /// <summary>How the money was received (for Deposit) or sent (for Refund). Null for Allocation/Adjustment.</summary>
    public ContributionMethod? Method { get; set; }

    /// <summary>For Allocation: which case the money was sent to.</summary>
    public int? CaseId { get; set; }
    public Case? Case { get; set; }

    /// <summary>For Allocation: the Contribution row created by this transaction.</summary>
    public int? ContributionId { get; set; }
    public Contribution? Contribution { get; set; }

    /// <summary>Admin who recorded this transaction. Audit only.</summary>
    public string? RecordedByUserId { get; set; }

    [StringLength(400)]
    public string? Notes { get; set; }

    /// <summary>
    /// When this transaction is an admin Correction, points back at the original (wrong)
    /// transaction it adjusts. Null for normal entries. Original rows are never mutated —
    /// the correction is a separate Adjustment carrying the delta and the reason.
    /// </summary>
    public int? CorrectsWalletTransactionId { get; set; }
    public WalletTransaction? CorrectsWalletTransaction { get; set; }
}
