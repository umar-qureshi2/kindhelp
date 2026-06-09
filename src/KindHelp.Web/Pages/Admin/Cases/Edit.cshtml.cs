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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();
    public Case? Case { get; private set; }

    public class InputModel
    {
        public int Id { get; set; }

        [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
        [Required, StringLength(160)] public string Slug { get; set; } = string.Empty;
        [Required, StringLength(400)] public string Summary { get; set; } = string.Empty;
        [Required] public string Description { get; set; } = string.Empty;
        public CaseCategory Category { get; set; }
        public CaseStatus Status { get; set; }
        [StringLength(120)] public string? Location { get; set; }
        public decimal? GoalAmount { get; set; }
        [StringLength(160)] public string? BeneficiaryName { get; set; }
        public bool ShowBeneficiaryName { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Case = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (Case is null) return NotFound();

        Input = new InputModel
        {
            Id = Case.Id,
            Title = Case.Title,
            Slug = Case.Slug,
            Summary = Case.Summary,
            Description = Case.Description,
            Category = Case.Category,
            Status = Case.Status,
            Location = Case.Location,
            GoalAmount = Case.GoalAmount,
            BeneficiaryName = Case.BeneficiaryName,
            ShowBeneficiaryName = Case.ShowBeneficiaryName
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Case = await _db.Cases.FirstOrDefaultAsync(c => c.Id == Input.Id, ct);
            return Page();
        }

        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == Input.Id, ct);
        if (entity is null) return NotFound();

        var newSlug = Slugger.Slugify(Input.Slug);
        if (newSlug != entity.Slug)
        {
            var baseSlug = newSlug;
            var n = 2;
            while (await _db.Cases.AnyAsync(c => c.Slug == newSlug && c.Id != entity.Id, ct))
                newSlug = $"{baseSlug}-{n++}";
        }

        var becameActive = entity.Status != CaseStatus.Active && Input.Status == CaseStatus.Active;
        var becameClosed = entity.Status != CaseStatus.Closed && Input.Status == CaseStatus.Closed;

        entity.Title = Input.Title;
        entity.Slug = newSlug;
        entity.Summary = Input.Summary;
        entity.Description = Input.Description;
        entity.Category = Input.Category;
        entity.Status = Input.Status;
        entity.Location = Input.Location;
        entity.GoalAmount = Input.GoalAmount;
        entity.BeneficiaryName = Input.BeneficiaryName;
        entity.ShowBeneficiaryName = Input.ShowBeneficiaryName;
        if (becameActive && entity.PublishedAtUtc is null) entity.PublishedAtUtc = DateTime.UtcNow;
        if (becameClosed) entity.ClosedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = "Case updated.";
        return RedirectToPage(new { id = entity.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken ct)
    {
        var entity = await _db.Cases
            .Include(c => c.Photos)
            .Include(c => c.Contributions)
            .FirstOrDefaultAsync(c => c.Id == Input.Id, ct);
        if (entity is null) return NotFound();

        if (entity.Contributions.Any())
        {
            TempData["StatusMessage"] = "This case has contributions; archive it instead of deleting to preserve donor records.";
            return RedirectToPage(new { id = entity.Id });
        }

        _db.Cases.Remove(entity);
        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = "Case deleted.";
        return RedirectToPage("/Admin/Cases/Index");
    }
}
