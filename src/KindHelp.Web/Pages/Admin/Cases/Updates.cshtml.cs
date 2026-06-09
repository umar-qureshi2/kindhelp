using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Data;
using KindHelp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin.Cases;

[Authorize(Policy = "AdminOnly")]
public class UpdatesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdatesModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public Case? Case { get; private set; }
    public IReadOnlyList<CaseUpdate> Updates { get; private set; } = Array.Empty<CaseUpdate>();

    public class InputModel
    {
        [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
        [Required] public string Body { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Case = await _db.Cases.FindAsync(new object?[] { id }, ct);
        if (Case is null) return NotFound();
        Updates = await _db.CaseUpdates
            .Where(u => u.CaseId == id)
            .OrderByDescending(u => u.PostedAtUtc)
            .ToListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int caseId, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync(caseId, ct);
            return Page();
        }

        var c = await _db.Cases.FindAsync(new object?[] { caseId }, ct);
        if (c is null) return NotFound();

        var update = new CaseUpdate
        {
            CaseId = caseId,
            Title = Input.Title,
            Body = Input.Body,
            PostedAtUtc = DateTime.UtcNow,
            PostedByUserId = _userManager.GetUserId(User)
        };
        _db.CaseUpdates.Add(update);
        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = "Update posted.";
        return RedirectToPage(new { id = caseId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int caseId, int updateId, CancellationToken ct)
    {
        var entity = await _db.CaseUpdates.FirstOrDefaultAsync(u => u.Id == updateId && u.CaseId == caseId, ct);
        if (entity is not null)
        {
            _db.CaseUpdates.Remove(entity);
            await _db.SaveChangesAsync(ct);
            TempData["StatusMessage"] = "Update deleted.";
        }
        return RedirectToPage(new { id = caseId });
    }
}
