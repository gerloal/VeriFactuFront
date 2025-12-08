using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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

    [BindProperty(SupportsGet = true)]
    public string? PaginationTrail { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CurrentToken { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Direction { get; set; }

    public InvoiceListResponseDto Result { get; private set; } = new();

    public bool HasNextPage => !string.IsNullOrWhiteSpace(Result?.ContinuationToken);

    public bool HasPreviousPage => BackwardTrail.Any();

    public string? PreviousToken => BackwardTrail.LastOrDefault();

    public string? PreviousTrailSerialized => BackwardTrail.Count > 0
        ? SerializeTrail(BackwardTrail.Take(BackwardTrail.Count - 1))
        : null;

    public string? NextTrailSerialized => SerializeTrail(BackwardTrailAppend(CurrentToken));

    private Queue<string> BackwardTrail { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            if (PageSize <= 0)
            {
                PageSize = DefaultPageSize;
            }

            BackwardTrail = DeserializeTrail(PaginationTrail);

            var tokenToUse = DetermineTokenToUse();
            CurrentToken = tokenToUse;

            Result = await _apiClient.GetInvoicesAsync(
                NumeroSerie,
                Emisor,
                Nif,
                Estado,
                FechaDesde,
                FechaHasta,
                PageSize,
                tokenToUse).ConfigureAwait(false);

            if (string.Equals(Direction, "next", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(tokenToUse))
            {
                BackwardTrail = new Queue<string>(BackwardTrailAppend(tokenToUse));
            }
            else if (string.Equals(Direction, "prev", StringComparison.OrdinalIgnoreCase)
                     && BackwardTrail.Count > 0)
            {
                BackwardTrail = new Queue<string>(BackwardTrail.Take(Math.Max(BackwardTrail.Count - 1, 0)));
            }

            PaginationTrail = SerializeTrail(BackwardTrail);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudieron recuperar las facturas.");
            ErrorMessage = "No se pudieron recuperar las facturas. Inténtelo de nuevo más tarde.";
            Result = new InvoiceListResponseDto();
        }

        return Page();
    }

    private string? DetermineTokenToUse()
    {
        if (string.Equals(Direction, "prev", StringComparison.OrdinalIgnoreCase))
        {
            if (BackwardTrail.Count == 0)
            {
                return null;
            }

            return BackwardTrail.Last();
        }

        if (!string.IsNullOrWhiteSpace(ContinuationToken))
        {
            return ContinuationToken;
        }

        if (!string.IsNullOrWhiteSpace(CurrentToken))
        {
            return CurrentToken;
        }

        return null;
    }

    private static Queue<string> DeserializeTrail(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return new Queue<string>();
        }

        try
        {
            var bytes = Convert.FromBase64String(serialized);
            var tokens = JsonSerializer.Deserialize<List<string>>(bytes) ?? new List<string>();
            return new Queue<string>(tokens.Where(t => !string.IsNullOrWhiteSpace(t)));
        }
        catch
        {
            return new Queue<string>();
        }
    }

    private static string? SerializeTrail(IEnumerable<string> tokens)
    {
        var list = tokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (list.Count == 0)
        {
            return null;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(list);
        return Convert.ToBase64String(bytes);
    }

    private IEnumerable<string> BackwardTrailAppend(string? token)
    {
        foreach (var t in BackwardTrail)
        {
            yield return t;
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            yield return token;
        }
    }
}
