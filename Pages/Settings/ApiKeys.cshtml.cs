using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Settings;

[Authorize]
public sealed class ApiKeysModel(VerifactuApiClient apiClient, ILogger<ApiKeysModel> logger) : PageModel
{
    private readonly VerifactuApiClient _apiClient = apiClient;
    private readonly ILogger<ApiKeysModel> _logger = logger;

    [BindProperty]
    [Display(Name = "Nombre de la clave")]
    [Required(ErrorMessage = "Introduce un nombre para la API Key.")]
    public string? NewApiKeyName { get; set; }

    public IList<ApiKeyDto>? ApiKeys { get; private set; }

    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadApiKeysAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadApiKeysAsync();
            return Page();
        }

        try
        {
            await _apiClient.CreateApiKeyAsync(NewApiKeyName!);
            StatusMessage = "API Key creada correctamente.";
            NewApiKeyName = string.Empty;
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("CreateApiKeyAsync is not implemented yet.");
            StatusMessage = "Esta acción aún no está disponible.";
        }

        await LoadApiKeysAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string apiKeyId)
    {
        if (string.IsNullOrWhiteSpace(apiKeyId))
        {
            ModelState.AddModelError(string.Empty, "Identificador de API Key no válido.");
            await LoadApiKeysAsync();
            return Page();
        }

        try
        {
            await _apiClient.DeleteApiKeyAsync(apiKeyId);
            StatusMessage = "API Key eliminada correctamente.";
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("DeleteApiKeyAsync is not implemented yet.");
            StatusMessage = "Esta acción aún no está disponible.";
        }

        await LoadApiKeysAsync();
        return Page();
    }

    private async Task LoadApiKeysAsync()
    {
        try
        {
            ApiKeys = await _apiClient.GetApiKeysAsync();
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("GetApiKeysAsync is not implemented yet.");
            ApiKeys = Array.Empty<ApiKeyDto>();
        }
    }
}
