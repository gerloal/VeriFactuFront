using System;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Batches;

[Authorize]
public sealed class IndexModel(VerifactuApiClient apiClient, ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 50;
    private readonly VerifactuApiClient _apiClient = apiClient;
    private readonly ILogger<IndexModel> _logger = logger;

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; private set; } = DefaultPageSize;

    public PagedResult<BatchDto>? PagedBatches { get; private set; }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PagedBatches is not null && PageNumber < PagedBatches.TotalPages;

    public int TotalPages => PagedBatches?.TotalPages ?? 0;

    public async Task OnGetAsync()
    {
        try
        {
            PagedBatches = await _apiClient.GetBatchesAsync(From, To, Status, PageNumber, PageSize);
            if (PagedBatches is not null)
            {
                PageSize = PagedBatches.PageSize;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudieron recuperar los lotes.");
            PagedBatches = new PagedResult<BatchDto>
            {
                Items = Array.Empty<BatchDto>(),
                Page = PageNumber,
                PageSize = PageSize,
                TotalCount = 0
            };
        }
    }
}
