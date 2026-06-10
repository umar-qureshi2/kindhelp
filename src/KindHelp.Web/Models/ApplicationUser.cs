using Microsoft.AspNetCore.Identity;

namespace KindHelp.Web.Models;

public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Display name shown only on the donor's own dashboard. Never shown to other donors
    /// because contributors must remain hidden from each other (privacy by design).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional phone for admin-side contact during offline donation reconciliation.
    /// Not exposed to other users.
    /// </summary>
    public string? ContactNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Linked donor profile (auto-created on registration). Null only briefly between
    /// account creation and first donor row insert. Contributions and wallet operations
    /// hang off the Donor entity, not directly off ApplicationUser.
    /// </summary>
    public Donor? Donor { get; set; }
}
