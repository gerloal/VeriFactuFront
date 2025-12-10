using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

    public PagedResult<BatchHistoryEntryDto>? PagedHistory { get; private set; }

    [BindProperty(SupportsGet = true, Name = "history")]
    public bool History { get; set; }

    public bool ShowHistory => History;

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => ShowHistory
        ? PagedHistory is not null && PageNumber < PagedHistory.TotalPages
        : PagedBatches is not null && PageNumber < PagedBatches.TotalPages;

    public int TotalPages => ShowHistory
        ? PagedHistory?.TotalPages ?? 0
        : PagedBatches?.TotalPages ?? 0;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        if (ShowHistory)
        {
            await LoadHistoryAsync();
        }
        else
        {
            await LoadPendingAsync();
        }
    }

    private async Task LoadPendingAsync()
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
            _logger.LogError(ex, "No se pudieron recuperar los lotes pendientes.");
            PagedBatches = new PagedResult<BatchDto>
            {
                Items = Array.Empty<BatchDto>(),
                Page = PageNumber,
                PageSize = PageSize,
                TotalCount = 0
            };
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            From ??= today.AddDays(-30);
            To ??= today;

            if (From > To)
            {
                (From, To) = (To, From);
            }

            var response = await _apiClient.GetBatchHistoryAsync(From, To).ConfigureAwait(false);
            var entries = response.Batches ?? new List<BatchHistoryEntryDto>();

            entries = entries
                .OrderByDescending(e => e.ReceivedAt)
                .ThenByDescending(e => e.CreatedAt)
                .ToList();

            if (!string.IsNullOrWhiteSpace(Status))
            {
                entries = entries
                    .Where(e => string.Equals(e.Status, Status, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var page = Math.Max(1, PageNumber);
            var pageSize = DefaultPageSize;
            var totalCount = entries.Count;
            var skip = (page - 1) * pageSize;
            var pageItems = entries.Skip(skip).Take(pageSize).ToList();

            PagedHistory = new PagedResult<BatchHistoryEntryDto>
            {
                Items = pageItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            PageSize = pageSize;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo recuperar el histórico de lotes.");
            PagedHistory = new PagedResult<BatchHistoryEntryDto>
            {
                Items = Array.Empty<BatchHistoryEntryDto>(),
                Page = PageNumber,
                PageSize = PageSize,
                TotalCount = 0
            };
        }
    }
}
