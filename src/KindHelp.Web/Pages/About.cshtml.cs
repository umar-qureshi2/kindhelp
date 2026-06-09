using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KindHelp.Web.Pages;

[AllowAnonymous]
public class AboutModel : PageModel
{
    public void OnGet() { }
}
