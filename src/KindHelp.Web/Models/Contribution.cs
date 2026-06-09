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
/// A donation recorded against a specific case for a specific donor.
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

    /// <summary>Donor's user id. Required — every donation must be tied to an account so the donor can see it on their own dashboard.</summary>
    [Required]
    public string DonorUserId { get; set; } = string.Empty;
    public ApplicationUser Donor { get; set; } = null!;

    [Range(0.01, 9_999_999_999)]
    public decimal Amount { get; set; }

    public ContributionMethod Method { get; set; } = ContributionMethod.BankTransfer;

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Free-form admin note (e.g. bank reference). Not visible to other donors.</summary>
    [StringLength(400)]
    public string? Reference { get; set; }

    /// <summary>Admin who recorded this entry. Audit only.</summary>
    public string? RecordedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
