using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Verifactu.Portal.Pages.Settings;

[Authorize]
public sealed class ApiKeysModel : PageModel
{
    public IActionResult OnGet() => Page();

    public IActionResult OnPostCreate() => NotFound();

    public IActionResult OnPostDelete(string apiKeyId) => NotFound();
}
