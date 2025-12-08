using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Verifactu.Portal.Pages.Account;

[AllowAnonymous]
public class SignOutCallbackModel : PageModel
{
    public void OnGet()
    {
    }
}
