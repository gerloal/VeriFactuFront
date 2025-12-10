using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Batches;

[Authorize]
public sealed class ItemResultModel(VerifactuApiClient apiClient, ILogger<ItemResultModel> logger) : PageModel
{
    private readonly VerifactuApiClient _apiClient = apiClient;
    private readonly ILogger<ItemResultModel> _logger = logger;

    [BindProperty(SupportsGet = true, Name = "batchId")]
    public string? BatchId { get; set; }

    [BindProperty(SupportsGet = true, Name = "itemId")]
    public string? ItemId { get; set; }

    public BatchItemResultSummary? Result { get; private set; }

    public string? PrettyPayload { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(ItemId))
        {
            StatusMessage = "No se indicó un identificador de elemento.";
            return RedirectToRelevantPage();
        }

        if (string.IsNullOrWhiteSpace(BatchId))
        {
            StatusMessage = "No se indicó el identificador del lote.";
            return RedirectToRelevantPage();
        }

        try
        {
            Result = await _apiClient.GetBatchItemResultSummaryAsync(BatchId, ItemId).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo recuperar el resultado del item {ItemId}", ItemId);
            StatusMessage = "No se pudo obtener el resultado del elemento. Inténtelo más tarde.";
            return RedirectToRelevantPage();
        }

        if (Result is null)
        {
            StatusMessage = "No se encontró información para el elemento especificado.";
            return RedirectToRelevantPage();
        }

        if (!string.IsNullOrWhiteSpace(Result.RawPayload))
        {
            PrettyPayload = TryFormatJson(Result.RawPayload!) ?? Result.RawPayload;
        }
        else if (TempData.TryGetValue(nameof(BatchItemResultSummary.RawPayload), out var raw) && raw is string rawText && !string.IsNullOrWhiteSpace(rawText))
        {
            PrettyPayload = TryFormatJson(rawText) ?? rawText;
        }

        return Page();
    }

    private static string? TryFormatJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private RedirectToPageResult RedirectToRelevantPage()
    {
        return string.IsNullOrWhiteSpace(BatchId)
            ? RedirectToPage("Index")
            : RedirectToPage("Details", new { batchId = BatchId });
    }
}
