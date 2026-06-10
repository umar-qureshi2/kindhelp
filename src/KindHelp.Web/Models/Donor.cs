using System.ComponentModel.DataAnnotations;

namespace KindHelp.Web.Models;

/// <summary>
/// Represents anyone who has given to a case — registered donor or one created on the fly
/// by an admin during contribution recording. The optional <see cref="ApplicationUserId"/>
/// links to a logged-in account when present. Privacy invariant: never expose donor identity
/// outside admin or that donor's own /My/* views.
/// </summary>
public class Donor
{
    public int Id { get; set; }

    [StringLength(120)]
    public string? DisplayName { get; set; }

    [StringLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(32)]
    public string? PhoneNumber { get; set; }

    /// <summary>Set when this donor has registered an account. Null for admin-managed donors.</summary>
    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    /// <summary>
    /// Denormalised running balance of the wallet. Always equals SUM(WalletTransactions.Amount).
    /// Updated atomically in <c>WalletService</c> alongside the corresponding transaction.
    /// </summary>
    public decimal WalletBalance { get; set; }

    [StringLength(800)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Admin who created this donor (for admin-managed donors). Audit only.</summary>
    public string? CreatedByUserId { get; set; }

    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();

    /// <summary>Best-effort display label for admin lists. Never shown to other donors.</summary>
    /// <remarks>
    /// Delegates to <see cref="DonorExtensions.LabelFor(Donor)"/> so the fallback chain has
    /// exactly one source of truth and matches what EF projects in queries.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Label => DonorExtensions.LabelFor(this);
}
