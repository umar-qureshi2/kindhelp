using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Data;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin.Cases;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [StringLength(160)]
        public string? Slug { get; set; }

        [Required, StringLength(400)]
        public string Summary { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full history & details")]
        public string Description { get; set; } = string.Empty;

        public CaseCategory Category { get; set; } = CaseCategory.Other;
        public CaseStatus Status { get; set; } = CaseStatus.Draft;

        [StringLength(120)]
        public string? Location { get; set; }

        [Display(Name = "Goal amount (optional)")]
        public decimal? GoalAmount { get; set; }

        [Display(Name = "Beneficiary name (internal)")]
        [StringLength(160)]
        public string? BeneficiaryName { get; set; }

        public bool ShowBeneficiaryName { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var slug = string.IsNullOrWhiteSpace(Input.Slug) ? Slugger.Slugify(Input.Title) : Slugger.Slugify(Input.Slug);

        // ensure uniqueness
        var baseSlug = slug;
        var n = 2;
        while (await _db.Cases.AnyAsync(c => c.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{n++}";
        }

        var entity = new Case
        {
            Title = Input.Title,
            Slug = slug,
            Summary = Input.Summary,
            Description = Input.Description,
            Category = Input.Category,
            Status = Input.Status,
            Location = Input.Location,
            GoalAmount = Input.GoalAmount,
            BeneficiaryName = Input.BeneficiaryName,
            ShowBeneficiaryName = Input.ShowBeneficiaryName,
            CreatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = Input.Status is CaseStatus.Active or CaseStatus.Funded ? DateTime.UtcNow : null
        };

        _db.Cases.Add(entity);
        await _db.SaveChangesAsync(ct);

        TempData["StatusMessage"] = "Case created.";
        return RedirectToPage("/Admin/Cases/Edit", new { id = entity.Id });
    }
}
