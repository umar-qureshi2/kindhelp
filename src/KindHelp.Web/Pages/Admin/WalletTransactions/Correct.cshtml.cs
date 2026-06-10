using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Data;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin.WalletTransactions;

[Authorize(Policy = "AdminOnly")]
public class CorrectModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IWalletService _wallet;
    private readonly UserManager<ApplicationUser> _userManager;

    public CorrectModel(ApplicationDbContext db, IWalletService wallet, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _wallet = wallet;
        _userManager = userManager;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public WalletTransaction? Original { get; private set; }
    public int DonorId { get; private set; }
    public string DonorLabel { get; private set; } = string.Empty;

    public class InputModel
    {
        public int TransactionId { get; set; }

        [Range(0.01, 9_999_999_999, ErrorMessage = "Corrected amount must be positive.")]
        public decimal CorrectedAmount { get; set; }

        [Required(ErrorMessage = "A reason is required.")]
        [StringLength(400, MinimumLength = 3, ErrorMessage = "Please provide at least a few words of context.")]
        public string Reason { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        await LoadOriginalAsync(id, ct);
        if (Original is null) return NotFound();

        Input.TransactionId = Original.Id;
        // Pre-fill with the existing absolute amount so the admin can just edit a digit.
        Input.CorrectedAmount = Math.Abs(Original.Amount);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadOriginalAsync(Input.TransactionId, ct);
        if (Original is null) return NotFound();

        if (!ModelState.IsValid) return Page();

        try
        {
            await _wallet.CorrectAmountAsync(
                Input.TransactionId,
                Input.CorrectedAmount,
                Input.Reason,
                _userManager.GetUserId(User) ?? string.Empty,
                ct);

            TempData["StatusMessage"] = $"Transaction #{Input.TransactionId} corrected.";
            return RedirectToPage("/Admin/Donors/Details", new { id = DonorId });
        }
        catch (InsufficientWalletBalanceException ex)
        {
            ModelState.AddModelError(nameof(Input.CorrectedAmount),
                $"This correction would push the wallet to {(ex.CurrentBalance - ex.Requested):N2}, which is negative. " +
                "Increase the corrected amount, record a deposit first, or use the manual adjust path.");
            return Page();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    private async Task LoadOriginalAsync(int id, CancellationToken ct)
    {
        Original = await _db.WalletTransactions
            .Include(t => t.Case)
            .Include(t => t.Donor)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (Original is null) return;

        DonorId = Original.DonorId;
        DonorLabel = Original.Donor?.Label ?? $"Donor #{Original.DonorId}";
    }
}
