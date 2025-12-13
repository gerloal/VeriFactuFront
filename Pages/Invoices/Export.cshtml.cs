using System;
using System.ComponentModel.DataAnnotations;
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
public sealed class ExportModel(VerifactuApiClient apiClient, ILogger<ExportModel> logger) : PageModel
{
    private readonly VerifactuApiClient _apiClient = apiClient;
    private readonly ILogger<ExportModel> _logger = logger;

    [BindProperty]
    [Display(Name = "Fecha desde")]
    [DataType(DataType.Date)]
    public DateTime? FechaDesde { get; set; }

    [BindProperty]
    [Display(Name = "Fecha hasta")]
    [DataType(DataType.Date)]
    public DateTime? FechaHasta { get; set; }

    [BindProperty]
    public string[]? Docs { get; set; }

    [BindProperty]
    public string? DocsCsv { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        var today = DateTime.Today;
        FechaHasta ??= today;
        FechaDesde ??= today.AddDays(-30);
    }

    public async Task<IActionResult> OnPostStartAsync()
    {
        try
        {
            var (from, to) = NormalizeDates();
            var docs = GetDocsValue();
            var job = await _apiClient.CreateInvoiceExportJobAsync(from, to, docs).ConfigureAwait(false);
            return BuildAjaxOk(job);
        }
        catch (InvalidOperationException ex)
        {
            return BuildAjaxError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al solicitar la exportación de facturas");
            return BuildAjaxError("No se pudo generar la exportación. Inténtalo de nuevo más tarde.");
        }
    }

    public async Task<IActionResult> OnGetStatusAsync([FromQuery] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BuildAjaxError("Falta el identificador del job.");
        }

        try
        {
            var job = await _apiClient.GetInvoiceExportJobStatusAsync(jobId).ConfigureAwait(false);
            return BuildAjaxOk(job);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al consultar el estado de la exportación de facturas");
            return BuildAjaxError("No se pudo consultar el estado de la exportación.");
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromForm] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BuildAjaxError("Falta el identificador del job.");
        }

        try
        {
            await _apiClient.DeleteInvoiceExportJobAsync(jobId).ConfigureAwait(false);
            return new JsonResult(new { deleted = true });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al borrar la exportación de facturas");
            return BuildAjaxError("No se pudo eliminar la exportación.");
        }
    }

    private IActionResult BuildAjaxOk(InvoiceExportJobStatusDto payload)
    {
        return new JsonResult(payload);
    }

    private IActionResult BuildAjaxError(string message)
    {
        ErrorMessage = message;
        return BadRequest(new { message });
    }

    private (DateTime? From, DateTime? To) NormalizeDates()
    {
        var from = FechaDesde?.Date;
        var to = FechaHasta?.Date;

        if (from.HasValue && to.HasValue && to < from)
        {
            return (to, from);
        }

        return (from, to);
    }

    private string? GetDocsValue()
    {
        var value = string.IsNullOrWhiteSpace(DocsCsv) ? null : DocsCsv.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            {
                return "all";
            }

            var parts = value
                .Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return parts.Length == 0 ? null : string.Join('|', parts);
        }

        if (Docs is null || Docs.Length == 0)
        {
            return null;
        }

        var normalized = Docs
            .Select(d => (d ?? string.Empty).Trim())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join('|', normalized);
    }
}
