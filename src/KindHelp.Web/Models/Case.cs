using System.ComponentModel.DataAnnotations;

namespace KindHelp.Web.Models;

public enum CaseStatus
{
    Draft = 0,
    Active = 1,
    Funded = 2,
    Closed = 3,
    Archived = 4
}

public enum CaseCategory
{
    Medical = 0,
    Education = 1,
    FoodAndShelter = 2,
    Disaster = 3,
    Orphan = 4,
    Other = 99
}

public class Case
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Short URL-safe identifier used in public links.</summary>
    [Required, StringLength(160)]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(400)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Full case history and details (markdown allowed; rendered as plain text in v1).</summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    public CaseCategory Category { get; set; } = CaseCategory.Other;

    public CaseStatus Status { get; set; } = CaseStatus.Draft;

    /// <summary>Beneficiary's location, e.g. "Karachi, Pakistan". Used for context, not for surfacing to other donors.</summary>
    [StringLength(120)]
    public string? Location { get; set; }

    /// <summary>Target amount in the configured currency. Nullable for open-ended causes.</summary>
    public decimal? GoalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>Beneficiary identifier or name kept internal — not displayed publicly unless explicitly enabled per case.</summary>
    [StringLength(160)]
    public string? BeneficiaryName { get; set; }

    public bool ShowBeneficiaryName { get; set; } = false;

    public ICollection<CasePhoto> Photos { get; set; } = new List<CasePhoto>();
    public ICollection<CaseUpdate> Updates { get; set; } = new List<CaseUpdate>();
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
}
