using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Verifactu.Portal.Pages.Account;

[AllowAnonymous]
public class SignedOutModel : PageModel
{
    public DateTimeOffset SignedOutAt { get; private set; }

    public string SignedOutAtDisplay { get; private set; } = string.Empty;

    public void OnGet()
    {
        SignedOutAt = DateTimeOffset.Now;
        SignedOutAtDisplay = SignedOutAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
    }
}
