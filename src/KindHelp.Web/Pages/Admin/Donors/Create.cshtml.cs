using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Admin.Donors;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly IDonorService _donors;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(IDonorService donors, UserManager<ApplicationUser> userManager)
    {
        _donors = donors;
        _userManager = userManager;
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [StringLength(120)]
        public string? DisplayName { get; set; }

        [EmailAddress, StringLength(256)]
        public string? Email { get; set; }

        [Phone, StringLength(32)]
        public string? PhoneNumber { get; set; }

        [StringLength(800)]
        public string? Notes { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        if (string.IsNullOrWhiteSpace(Input.Email) && string.IsNullOrWhiteSpace(Input.PhoneNumber))
        {
            ModelState.AddModelError(string.Empty, "Either an email or a phone number is required.");
            return Page();
        }

        try
        {
            var donor = await _donors.CreateAdminManagedAsync(
                Input.DisplayName, Input.Email, Input.PhoneNumber, Input.Notes,
                _userManager.GetUserId(User) ?? string.Empty, ct);
            TempData["StatusMessage"] = $"Donor created: {donor.Label}";
            return RedirectToPage("/Admin/Donors/Details", new { id = donor.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }
}
