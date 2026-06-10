using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Admin.Donors;

[Authorize(Policy = "AdminOnly")]
public class DepositModel : PageModel
{
    private readonly IDonorService _donors;
    private readonly IWalletService _wallet;
    private readonly UserManager<ApplicationUser> _userManager;

    public DepositModel(IDonorService donors, IWalletService wallet, UserManager<ApplicationUser> userManager)
    {
        _donors = donors;
        _wallet = wallet;
        _userManager = userManager;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public Donor? Donor { get; private set; }

    public class InputModel
    {
        [Range(0.01, 9_999_999_999)] public decimal Amount { get; set; }
        public ContributionMethod Method { get; set; } = ContributionMethod.BankTransfer;
        [DataType(DataType.Date)] public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow.Date;
        [StringLength(400)] public string? Reference { get; set; }
        [StringLength(400)] public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Donor = await _donors.GetByIdAsync(id, ct);
        if (Donor is null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        Donor = await _donors.GetByIdAsync(id, ct);
        if (Donor is null) return NotFound();

        if (!ModelState.IsValid) return Page();

        try
        {
            await _wallet.DepositAsync(
                id,
                Input.Amount,
                Input.Method,
                DateTime.SpecifyKind(Input.OccurredAtUtc, DateTimeKind.Utc),
                Input.Reference,
                Input.Notes,
                _userManager.GetUserId(User) ?? string.Empty,
                ct);

            TempData["StatusMessage"] = $"Deposit of {Input.Amount:N0} recorded for {Donor.Label}.";
            return RedirectToPage("/Admin/Donors/Details", new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }
}
