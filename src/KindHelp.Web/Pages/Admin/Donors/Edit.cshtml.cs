using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Admin.Donors;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly IDonorService _donors;
    public EditModel(IDonorService donors) => _donors = donors;

    [BindProperty] public InputModel Input { get; set; } = new();
    public Donor? Donor { get; private set; }

    public class InputModel
    {
        public int Id { get; set; }
        [StringLength(120)] public string? DisplayName { get; set; }
        [EmailAddress, StringLength(256)] public string? Email { get; set; }
        [Phone, StringLength(32)] public string? PhoneNumber { get; set; }
        [StringLength(800)] public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Donor = await _donors.GetByIdAsync(id, ct);
        if (Donor is null) return NotFound();
        Input = new InputModel
        {
            Id = Donor.Id,
            DisplayName = Donor.DisplayName,
            Email = Donor.Email,
            PhoneNumber = Donor.PhoneNumber,
            Notes = Donor.Notes
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Donor = await _donors.GetByIdAsync(Input.Id, ct);
        if (Donor is null) return NotFound();

        if (!ModelState.IsValid) return Page();

        try
        {
            await _donors.UpdateAsync(Input.Id, Input.DisplayName, Input.Email, Input.PhoneNumber, Input.Notes, ct);
            TempData["StatusMessage"] = "Donor updated.";
            return RedirectToPage("/Admin/Donors/Details", new { id = Input.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }
}
