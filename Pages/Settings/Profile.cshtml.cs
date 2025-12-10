using System.Net;
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

    public RemoteUserStatusDto? RemoteStatus { get; private set; }

    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            EditableProfile = await _apiClient.GetProfileAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(ex, "Forbidden while retrieving profile information.");
            StatusMessage = "No tienes permisos para consultar el perfil de la compañía.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Unexpected HTTP error retrieving profile information.");
            StatusMessage = "No se pudo recuperar el perfil. Inténtalo de nuevo más tarde.";
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("GetProfileAsync is not implemented yet.");
        }

        try
        {
            RemoteStatus = await _apiClient.GetRemoteUserStatusAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo recuperar la información remota del usuario.");
        }
    }
}
