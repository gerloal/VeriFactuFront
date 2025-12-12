using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public async Task<IActionResult> OnPostAsync()
    {
        if (!FechaDesde.HasValue)
        {
            ModelState.AddModelError(nameof(FechaDesde), "Debes seleccionar una fecha de inicio.");
        }

        if (!FechaHasta.HasValue)
        {
            ModelState.AddModelError(nameof(FechaHasta), "Debes seleccionar una fecha de fin.");
        }

        if (!ModelState.IsValid)
        {
            return BuildValidationErrorResult();
        }

        var from = FechaDesde!.Value.Date;
        var to = FechaHasta!.Value.Date;

        if (to < from)
        {
            (from, to) = (to, from);
            FechaDesde = from;
            FechaHasta = to;
        }

        try
        {
            var docsCsv = GetDocsCsv();
            var exportResult = await _apiClient.DownloadInvoicesXmlAsync(from, to, docsCsv).ConfigureAwait(false);
            if (exportResult is null || exportResult.Content.Length == 0)
            {
                return BuildErrorResult("No se encontraron facturas para el periodo indicado.");
            }

            return File(exportResult.Content, exportResult.ContentType, exportResult.FileName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al solicitar la exportación de facturas");
            return BuildErrorResult("No se pudo generar la exportación. Inténtalo de nuevo más tarde.");
        }
    }

    private IActionResult BuildValidationErrorResult()
    {
        if (IsAjaxRequest())
        {
            var messages = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => (e.ErrorMessage ?? string.Empty).Trim())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            var message = messages.Count > 0
                ? string.Join(" ", messages)
                : "Debes corregir los errores del formulario.";

            return BadRequest(new { message });
        }

        return Page();
    }

    private IActionResult BuildErrorResult(string message)
    {
        ErrorMessage = message;

        if (IsAjaxRequest())
        {
            return BadRequest(new { message });
        }

        return Page();
    }

    private bool IsAjaxRequest()
    {
        if (Request is null)
        {
            return false;
        }

        if (!Request.Headers.TryGetValue("X-Requested-With", out var headerValues))
        {
            return false;
        }

        return string.Equals(headerValues.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetDocsCsv()
    {
        if (!string.IsNullOrWhiteSpace(DocsCsv))
        {
            return DocsCsv.Trim();
        }

        if (Docs is null || Docs.Length == 0)
        {
            return null;
        }

        var normalized = Docs
            .Select(d => (d ?? string.Empty).Trim())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(',', normalized);
    }
}
