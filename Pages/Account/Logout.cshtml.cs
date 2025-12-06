using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Verifactu.Portal.Pages.Account;

[AllowAnonymous]
public sealed class LogoutModel : PageModel
{
    public IActionResult OnPost()
    {
        var redirectUri = Url.Page("/Index", null, null, Request.Scheme)
                          ?? Url.Content("~/");

        var authProperties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return SignOut(authProperties, CookieAuthenticationDefaults.AuthenticationScheme, "Cognito");
    }
}
