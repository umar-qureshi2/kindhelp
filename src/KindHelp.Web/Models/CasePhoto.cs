using System.ComponentModel.DataAnnotations;

namespace KindHelp.Web.Models;

public class CasePhoto
{
    public int Id { get; set; }

    public int CaseId { get; set; }
    public Case Case { get; set; } = null!;

    /// <summary>Path relative to wwwroot, e.g. "/uploads/cases/12/cover.jpg".</summary>
    [Required, StringLength(300)]
    public string RelativePath { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Caption { get; set; }

    public bool IsCover { get; set; }

    public int SortOrder { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
