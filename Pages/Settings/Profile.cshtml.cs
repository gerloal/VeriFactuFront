using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Settings;

[Authorize]
public sealed class ProfileModel(VerifactuApiClient apiClient, ILogger<ProfileModel> logger) : PageModel
{
    private readonly VerifactuApiClient _apiClient = apiClient;
    private readonly ILogger<ProfileModel> _logger = logger;

    [BindProperty]
    public ProfileDto EditableProfile { get; set; } = new()
    {
        TenantId = string.Empty,
        CompanyName = string.Empty,
        Nif = string.Empty,
        Email = string.Empty
    };

    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            EditableProfile = await _apiClient.GetProfileAsync();
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("GetProfileAsync is not implemented yet.");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _apiClient.UpdateProfileAsync(EditableProfile);
            StatusMessage = "Perfil actualizado correctamente.";
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("UpdateProfileAsync is not implemented yet.");
            StatusMessage = "Esta acción aún no está disponible.";
        }

        return Page();
    }
}
