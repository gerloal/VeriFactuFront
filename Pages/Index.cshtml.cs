using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Verifactu.Portal.Pages;

[Authorize]
public class IndexModel(ILogger<IndexModel> logger) : PageModel
{
    private readonly ILogger<IndexModel> _logger = logger;

    public void OnGet()
    {
        _logger.LogDebug("Index page visited by {User}", User.Identity?.Name);
    }
}
