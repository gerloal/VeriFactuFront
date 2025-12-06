using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Invoices;

[Authorize]
public sealed class DetailsModel : PageModel
{
    private readonly VerifactuApiClient _apiClient;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(VerifactuApiClient apiClient, ILogger<DetailsModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string FacturaId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? IdempotencyKey { get; set; }

    public InvoiceDetailDto? Invoice { get; private set; }

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(FacturaId))
        {
            ErrorMessage = "Debe indicar una factura.";
            return Page();
        }

        try
        {
            Invoice = await _apiClient.GetInvoiceDetailAsync(FacturaId, IdempotencyKey).ConfigureAwait(false);
            if (Invoice is null)
            {
                ErrorMessage = "No se encontró la factura solicitada.";
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo recuperar la factura {FacturaId}", FacturaId);
            ErrorMessage = "No se pudo recuperar la factura. Inténtelo de nuevo más tarde.";
        }

        return Page();
    }

    public async Task<IActionResult> OnGetDownloadAsync(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(FacturaId))
        {
            StatusMessage = "Factura no especificada.";
            return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
        }

        if (string.IsNullOrWhiteSpace(documentType))
        {
            StatusMessage = "Tipo de documento no válido.";
            return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
        }

        try
        {
            var document = await _apiClient.GetInvoiceDocumentAsync(FacturaId, documentType, IdempotencyKey).ConfigureAwait(false);
            if (document is null || string.IsNullOrWhiteSpace(document.Url))
            {
                StatusMessage = "El documento solicitado no está disponible.";
                return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
            }

            StatusMessage = $"Enlace disponible hasta {document.ExpiresAt:u}";
            return Redirect(document.Url);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo generar la descarga del documento {Tipo} para la factura {FacturaId}", documentType, FacturaId);
            StatusMessage = "No se pudo descargar el documento. Inténtelo de nuevo más tarde.";
            return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
        }
    }
}
