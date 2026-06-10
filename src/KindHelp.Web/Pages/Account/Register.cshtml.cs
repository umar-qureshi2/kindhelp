using System.ComponentModel.DataAnnotations;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IDonorService _donors;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IDonorService donors,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _donors = donors;
        _logger = logger;
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, StringLength(80)]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            DisplayName = Input.DisplayName
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, Roles.Donor);

            // Create (or claim an admin-managed) Donor row + wallet, linked to this user.
            await _donors.EnsureForUserAsync(user);

            _logger.LogInformation("New donor account created: {Email}", Input.Email);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToPage("/My/Dashboard");
        }

        foreach (var err in result.Errors)
            ModelState.AddModelError(string.Empty, err.Description);

        return Page();
    }
}
