using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Data;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin.Contributions;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IContributionService _contributions;

    public CreateModel(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IContributionService contributions)
    {
        _db = db;
        _userManager = userManager;
        _contributions = contributions;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public IReadOnlyList<Case> Cases { get; private set; } = Array.Empty<Case>();

    public class InputModel
    {
        [Required] public int CaseId { get; set; }

        [Required, EmailAddress]
        [Display(Name = "Donor email")]
        public string DonorEmail { get; set; } = string.Empty;

        [Range(0.01, 9_999_999_999)]
        public decimal Amount { get; set; }

        public ContributionMethod Method { get; set; } = ContributionMethod.BankTransfer;

        [DataType(DataType.Date)]
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow.Date;

        [StringLength(400)]
        public string? Reference { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Cases = await _db.Cases
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await OnGetAsync(ct);
        if (!ModelState.IsValid) return Page();

        var donor = await _userManager.FindByEmailAsync(Input.DonorEmail);
        if (donor is null)
        {
            ModelState.AddModelError(nameof(Input.DonorEmail), "No account found with that email. Ask the donor to register first.");
            return Page();
        }

        var caseExists = await _db.Cases.AnyAsync(c => c.Id == Input.CaseId, ct);
        if (!caseExists)
        {
            ModelState.AddModelError(nameof(Input.CaseId), "Selected case not found.");
            return Page();
        }

        var entity = new Contribution
        {
            CaseId = Input.CaseId,
            DonorUserId = donor.Id,
            Amount = Input.Amount,
            Method = Input.Method,
            ReceivedAtUtc = DateTime.SpecifyKind(Input.ReceivedAtUtc, DateTimeKind.Utc),
            Reference = Input.Reference
        };

        var recordedBy = _userManager.GetUserId(User) ?? string.Empty;
        await _contributions.RecordAsync(entity, recordedBy, ct);

        TempData["StatusMessage"] = "Contribution recorded.";
        return RedirectToPage("/Admin/Contributions/Index", new { caseId = Input.CaseId });
    }
}
