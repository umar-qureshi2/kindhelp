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
    private readonly IDonorService _donors;
    private readonly IWalletService _wallet;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(
        ApplicationDbContext db,
        IDonorService donors,
        IWalletService wallet,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _donors = donors;
        _wallet = wallet;
        _userManager = userManager;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public IReadOnlyList<DonorPick> Donors { get; private set; } = Array.Empty<DonorPick>();
    public IReadOnlyList<Case> Cases { get; private set; } = Array.Empty<Case>();

    /// <summary>Repopulates the datalist input on validation error / preselect.</summary>
    public string SelectedDonorLabel { get; private set; } = string.Empty;

    public record DonorPick(int Id, string Label, decimal WalletBalance);

    public class InputModel
    {
        public int DonorId { get; set; }

        [EmailAddress, StringLength(256)]
        public string? NewDonorEmail { get; set; }

        [Phone, StringLength(32)]
        public string? NewDonorPhone { get; set; }

        [StringLength(120)]
        public string? NewDonorName { get; set; }

        public int CaseId { get; set; }

        [Range(0.01, 9_999_999_999)]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow.Date;

        [StringLength(400)]
        public string? Reference { get; set; }
    }

    public async Task OnGetAsync(int? donorId, CancellationToken ct)
    {
        if (donorId.HasValue) Input.DonorId = donorId.Value;
        await LoadPickListsAsync(ct);
        SyncSelectedLabel();
    }

    private void SyncSelectedLabel()
    {
        if (Input.DonorId > 0)
        {
            var pick = Donors.FirstOrDefault(d => d.Id == Input.DonorId);
            if (pick is not null)
                SelectedDonorLabel = $"{pick.Label} — wallet {pick.WalletBalance:N0}";
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadPickListsAsync(ct);
        SyncSelectedLabel();

        if (Input.CaseId <= 0)
        {
            ModelState.AddModelError(nameof(Input.CaseId), "Please choose a case.");
        }
        if (!ModelState.IsValid) return Page();

        // Resolve donor: pick from list, OR find existing by email/phone, OR create new.
        Donor? donor = null;
        if (Input.DonorId > 0)
        {
            donor = await _donors.GetByIdAsync(Input.DonorId, ct);
            if (donor is null)
            {
                ModelState.AddModelError(nameof(Input.DonorId), "Selected donor not found.");
                return Page();
            }
        }
        else
        {
            // Try to find by contact first; create if no match.
            donor = await _donors.FindByContactAsync(Input.NewDonorEmail, Input.NewDonorPhone, ct);
            if (donor is null)
            {
                if (string.IsNullOrWhiteSpace(Input.NewDonorEmail) && string.IsNullOrWhiteSpace(Input.NewDonorPhone))
                {
                    ModelState.AddModelError(string.Empty, "Pick an existing donor or provide a new donor's email or phone.");
                    return Page();
                }
                try
                {
                    donor = await _donors.CreateAdminManagedAsync(
                        Input.NewDonorName, Input.NewDonorEmail, Input.NewDonorPhone, null,
                        _userManager.GetUserId(User) ?? string.Empty, ct);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    return Page();
                }
            }
        }

        try
        {
            var contribution = await _wallet.AllocateAsync(
                donor.Id,
                Input.CaseId,
                Input.Amount,
                DateTime.SpecifyKind(Input.ReceivedAtUtc, DateTimeKind.Utc),
                Input.Reference,
                _userManager.GetUserId(User) ?? string.Empty,
                ct);

            TempData["StatusMessage"] = $"Allocated {Input.Amount:N0} from {donor.Label} to the selected case.";
            return RedirectToPage("/Admin/Contributions/Index", new { caseId = Input.CaseId });
        }
        catch (InsufficientWalletBalanceException ex)
        {
            ModelState.AddModelError(nameof(Input.Amount),
                $"{donor.Label}'s wallet has only {ex.CurrentBalance:N0} but {ex.Requested:N0} was requested. Add a deposit first.");
            return Page();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    private async Task LoadPickListsAsync(CancellationToken ct)
    {
        // No artificial cap: the page renders a native <datalist> that the browser filters
        // as the admin types, so even thousands of donors stay usable. We do order by
        // wallet balance + label so the most-likely picks surface first.
        Donors = await _db.Donors
            .AsNoTracking()
            .OrderByDescending(d => d.WalletBalance)
            .ThenBy(d => d.DisplayName ?? d.Email ?? d.PhoneNumber)
            .Select(d => new DonorPick(
                d.Id,
                d.DisplayName ?? d.Email ?? d.PhoneNumber ?? ("Donor #" + d.Id),
                d.WalletBalance))
            .ToListAsync(ct);

        Cases = await _db.Cases
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .ToListAsync(ct);
    }
}
