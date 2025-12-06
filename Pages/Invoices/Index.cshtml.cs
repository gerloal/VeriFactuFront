using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Invoices;

[Authorize]
public sealed class IndexModel : PageModel
{
    private const int DefaultPageSize = 50;
    private readonly VerifactuApiClient _apiClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(VerifactuApiClient apiClient, ILogger<IndexModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Emisor { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Nif { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? NumeroSerie { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FechaDesde { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FechaHasta { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string? ContinuationToken { get; set; }

    public InvoiceListResponseDto Result { get; private set; } = new();

    public bool HasNextPage => !string.IsNullOrWhiteSpace(Result?.ContinuationToken);

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            if (PageSize <= 0)
            {
                PageSize = DefaultPageSize;
            }

            Result = await _apiClient.GetInvoicesAsync(
                NumeroSerie,
                Emisor,
                Nif,
                Estado,
                FechaDesde,
                FechaHasta,
                PageSize,
                ContinuationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudieron recuperar las facturas.");
            ErrorMessage = "No se pudieron recuperar las facturas. Inténtelo de nuevo más tarde.";
            Result = new InvoiceListResponseDto();
        }
    }
}
