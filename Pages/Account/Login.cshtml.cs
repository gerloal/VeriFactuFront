using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Verifactu.Portal.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        var redirectUri = string.IsNullOrEmpty(returnUrl) ? Url.Content("~/") : returnUrl;
        var authProperties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return Challenge(authProperties, "Cognito");
    }
}
