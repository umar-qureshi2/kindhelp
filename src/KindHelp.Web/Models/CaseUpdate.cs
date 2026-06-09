using System.ComponentModel.DataAnnotations;

namespace KindHelp.Web.Models;

/// <summary>
/// An update posted by an admin against a case (progress, milestone, thank-you note, etc.).
/// Visible publicly on the case page and in donor dashboards for donors who supported the case.
/// </summary>
public class CaseUpdate
{
    public int Id { get; set; }

    public int CaseId { get; set; }
    public Case Case { get; set; } = null!;

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional admin who posted the update; for audit only.</summary>
    public string? PostedByUserId { get; set; }
}
