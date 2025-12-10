using System;
using System.ComponentModel.DataAnnotations;
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
            return Page();
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
            var exportResult = await _apiClient.DownloadInvoicesXmlAsync(from, to).ConfigureAwait(false);
            if (exportResult is null || exportResult.Content.Length == 0)
            {
                ErrorMessage = "No se encontraron facturas para el periodo indicado.";
                return Page();
            }

            return File(exportResult.Content, exportResult.ContentType, exportResult.FileName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al solicitar la exportación de facturas");
            ErrorMessage = "No se pudo generar la exportación. Inténtalo de nuevo más tarde.";
            return Page();
        }
    }
}
