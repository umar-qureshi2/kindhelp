using System.ComponentModel.DataAnnotations;

namespace KindHelp.Web.Models;

public enum ContributionMethod
{
    Cash = 0,
    BankTransfer = 1,
    MobileWallet = 2,
    Cheque = 3,
    InKind = 4,
    Other = 99
}

/// <summary>
/// Money allocated from a donor's wallet to a specific case. Every contribution is created
/// by a <see cref="WalletTransaction"/> of type <c>Allocation</c>; the link is two-way.
/// The original payment method lives on the originating <c>Deposit</c> WalletTransaction —
/// not duplicated here.
///
/// PRIVACY RULE: This record must NEVER be exposed to any user other than:
///   (a) the donor themselves (own dashboard), or
///   (b) an admin (admin pages).
/// Public case pages may show only the SUM of Amount across contributions and a count of donors.
/// </summary>
public class Contribution
{
    public int Id { get; set; }

    public int CaseId { get; set; }
    public Case Case { get; set; } = null!;

    /// <summary>The donor whose wallet funded this contribution.</summary>
    public int DonorId { get; set; }
    public Donor Donor { get; set; } = null!;

    [Range(0.01, 9_999_999_999)]
    public decimal Amount { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Free-form admin note (e.g. bank reference). Not visible to other donors.</summary>
    [StringLength(400)]
    public string? Reference { get; set; }

    /// <summary>Admin who recorded this entry. Audit only.</summary>
    public string? RecordedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The WalletTransaction (type=Allocation) that debited the donor's wallet to fund this contribution.</summary>
    public int? WalletTransactionId { get; set; }
    public WalletTransaction? WalletTransaction { get; set; }
}
