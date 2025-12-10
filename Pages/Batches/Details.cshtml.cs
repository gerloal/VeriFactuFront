using System;
using System.Collections.Generic;
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

    public IList<InvoiceDto> PendingInvoices { get; private set; } = new List<InvoiceDto>();

    public bool CanUseRemoteCertificate { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

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

        try
        {
            PendingInvoices = await _apiClient.GetBatchItemsAsync(BatchId, "Pending");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudieron recuperar las facturas pendientes del lote {BatchId}", BatchId);
            PendingInvoices = new List<InvoiceDto>();
        }

        try
        {
            var remoteStatus = await _apiClient.GetRemoteUserStatusAsync().ConfigureAwait(false);
            CanUseRemoteCertificate = remoteStatus.RemoteCertificateEnabled;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo determinar si el tenant puede usar certificado remoto.");
            CanUseRemoteCertificate = false;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (string.IsNullOrWhiteSpace(BatchId))
        {
            StatusMessage = "No se pudo identificar el lote a eliminar.";
            return RedirectToPage("Index");
        }

        try
        {
            await _apiClient.DeleteBatchAsync(BatchId).ConfigureAwait(false);
            StatusMessage = $"Lote {BatchId} eliminado correctamente.";
            return RedirectToPage("Index");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error eliminando el lote {BatchId}", BatchId);
            StatusMessage = "No se pudo eliminar el lote. Inténtalo de nuevo más tarde.";
            return RedirectToPage(new { batchId = BatchId });
        }
    }

    public async Task<IActionResult> OnPostViewInvoiceAsync(string itemId, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(BatchId))
        {
            StatusMessage = "No se pudo identificar el lote.";
            return RedirectToPage("Index");
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            StatusMessage = "Elemento no válido.";
            return RedirectToPage(new { batchId = BatchId });
        }

        try
        {
            var summary = await _apiClient.GetBatchItemResultSummaryAsync(BatchId, itemId).ConfigureAwait(false);
            if (summary is null)
            {
                StatusMessage = "No se encontró información para la factura seleccionada.";
                return RedirectToPage(new { batchId = BatchId });
            }

            if (!string.IsNullOrWhiteSpace(summary.FacturaId))
            {
                var redirectIdempotency = summary.IdempotencyKey ?? idempotencyKey;
                return RedirectToPage("/Invoices/Details", new { facturaId = summary.FacturaId, idempotencyKey = redirectIdempotency });
            }

            if (!string.IsNullOrWhiteSpace(summary.RawPayload))
            {
                TempData[nameof(BatchItemResultSummary.RawPayload)] = summary.RawPayload;
            }

            StatusMessage = "No se pudo derivar el identificador de factura. Mostrando el resultado bruto.";
            return RedirectToPage("ItemResult", new { batchId = BatchId, itemId });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo obtener el detalle de la factura {ItemId} del lote {BatchId}", itemId, BatchId);
            StatusMessage = "No se pudo obtener la factura solicitada. Inténtalo más tarde.";
            return RedirectToPage(new { batchId = BatchId });
        }
    }

    public async Task<IActionResult> OnPostResumeAsync()
    {
        if (string.IsNullOrWhiteSpace(BatchId))
        {
            StatusMessage = "No se pudo identificar el lote.";
            return RedirectToPage("Index");
        }

        try
        {
            var result = await _apiClient.ResumeBatchAsync(BatchId).ConfigureAwait(false);
            if (result?.Summary is not null)
            {
                StatusMessage = $"Reanudación solicitada. Correctas: {result.Summary.Succeeded}/{result.Summary.TotalInvoices}. Fallos: {result.Summary.Failed}.";
            }
            else
            {
                StatusMessage = "Reanudación solicitada. Revisa los logs para más detalles.";
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error reanudando el lote {BatchId} mediante certificado remoto.", BatchId);
            StatusMessage = "No se pudo reanudar el lote. Comprueba que el certificado remoto está autorizado.";
        }

        return RedirectToPage(new { batchId = BatchId });
    }
}
