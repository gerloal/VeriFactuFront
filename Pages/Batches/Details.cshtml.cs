using System;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Batches;

[Authorize]
public sealed class DetailsModel(VerifactuApiClient apiClient, ILogger<DetailsModel> logger) : PageModel
{
    private readonly VerifactuApiClient _apiClient = apiClient;
    private readonly ILogger<DetailsModel> _logger = logger;

    [BindProperty(SupportsGet = true, Name = "batchId")]
    public string? BatchId { get; set; }

    public BatchDetailDto? Batch { get; private set; }

    public PagedResult<InvoiceDto>? Invoices { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(BatchId))
        {
            return NotFound();
        }

        try
        {
            Batch = await _apiClient.GetBatchByIdAsync(BatchId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo recuperar la información del lote {BatchId}", BatchId);
            Batch = null;
        }

        if (Batch?.Items is { Count: > 0 })
        {
            var items = Batch.Items;
            Invoices = new PagedResult<InvoiceDto>
            {
                Items = items,
                Page = 1,
                PageSize = Math.Min(items.Count, 100),
                TotalCount = items.Count
            };
        }
        else
        {
            Invoices = new PagedResult<InvoiceDto>
            {
                Items = Array.Empty<InvoiceDto>(),
                Page = 1,
                PageSize = 0,
                TotalCount = 0
            };
        }

        return Page();
    }
}
