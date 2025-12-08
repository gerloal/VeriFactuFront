using System;
using System.IO;
using System.Linq;
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
    private readonly IQrCodeRenderer _qrCodeRenderer;
    private static readonly HttpClient QrHttpClient = new();

    public DetailsModel(VerifactuApiClient apiClient, ILogger<DetailsModel> logger, IQrCodeRenderer qrCodeRenderer)
    {
        _apiClient = apiClient;
        _logger = logger;
        _qrCodeRenderer = qrCodeRenderer;
    }

    [BindProperty(SupportsGet = true)]
    public string FacturaId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? IdempotencyKey { get; set; }

    public InvoiceDetailDto? Invoice { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? QrImageBase64 { get; private set; }

    public string QrDownloadFileName => Invoice is null ? "factura-qr.png" : BuildQrFileName(Invoice);

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
            else
            {
                QrImageBase64 = await ResolveQrBase64Async(Invoice).ConfigureAwait(false);
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

    public async Task<IActionResult> OnGetDownloadQrAsync()
    {
        if (string.IsNullOrWhiteSpace(FacturaId))
        {
            StatusMessage = "Factura no especificada.";
            return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
        }

        try
        {
            var invoice = await _apiClient.GetInvoiceDetailAsync(FacturaId, IdempotencyKey).ConfigureAwait(false);
            if (invoice is null)
            {
                StatusMessage = "No se encontró la factura solicitada.";
                return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
            }

            var base64 = await ResolveQrBase64Async(invoice).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(base64))
            {
                StatusMessage = "El código QR no está disponible para esta factura.";
                return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "El código QR recibido para la factura {FacturaId} no es válido.", invoice.FacturaId);
                StatusMessage = "El código QR no está disponible actualmente.";
                return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
            }

            var fileName = BuildQrFileName(invoice);
            return File(bytes, "image/png", fileName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo descargar el código QR de la factura {FacturaId}", FacturaId);
            StatusMessage = "No se pudo descargar el código QR. Inténtalo de nuevo más tarde.";
            return RedirectToPage(new { facturaId = FacturaId, idempotencyKey = IdempotencyKey });
        }
    }

    private async Task<string?> ResolveQrBase64Async(InvoiceDetailDto invoice)
    {
        if (!string.IsNullOrWhiteSpace(invoice.QrPngBase64))
        {
            return invoice.QrPngBase64;
        }

        var storageBase64 = await TryDownloadQrFromStorageAsync(invoice).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(storageBase64))
        {
            return storageBase64;
        }

        if (!string.IsNullOrWhiteSpace(invoice.QrUrl))
        {
            try
            {
                return _qrCodeRenderer.GeneratePngBase64(invoice.QrUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo generar la imagen del código QR para la factura {FacturaId}.", invoice.FacturaId);
            }
        }

        return null;
    }

    private async Task<string?> TryDownloadQrFromStorageAsync(InvoiceDetailDto invoice)
    {
        var storageKey = invoice.QrS3Key
                          ?? invoice.Ledger?.QrKey
                          ?? invoice.Documentos?.QrKey;

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        try
        {
            var document = await _apiClient
                .GetInvoiceDocumentAsync(invoice.FacturaId, "qr", invoice.IdempotencyKey)
                .ConfigureAwait(false);

            if (document is null || string.IsNullOrWhiteSpace(document.Url))
            {
                _logger.LogDebug("No se obtuvo URL firmada para el QR de la factura {FacturaId}.", invoice.FacturaId);
                return null;
            }

            using var response = await QrHttpClient.GetAsync(document.Url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo descargar el código QR desde almacenamiento para la factura {FacturaId}. Estado {StatusCode}", invoice.FacturaId, response.StatusCode);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            return Convert.ToBase64String(bytes);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo recuperar el código QR desde almacenamiento para la factura {FacturaId}.", invoice.FacturaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado descargando el código QR para la factura {FacturaId}.", invoice.FacturaId);
        }

        return null;
    }

    private static string BuildQrFileName(InvoiceDetailDto invoice)
    {
        var rawName = string.IsNullOrWhiteSpace(invoice.NumeroSerie)
            ? invoice.FacturaId
            : invoice.NumeroSerie;

        if (string.IsNullOrWhiteSpace(rawName))
        {
            rawName = "factura";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawName
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "factura";
        }

        return $"{sanitized}-qr.png";
    }
}
