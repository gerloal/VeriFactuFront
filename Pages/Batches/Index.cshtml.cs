using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Verifactu.Portal.Models;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Batches;

[Authorize]
public sealed class IndexModel(VerifactuApiClient apiClient, ILogger<IndexModel> logger, ITenantContext tenantContext) : PageModel
{
    private const int DefaultPageSize = 50;
    private static readonly JsonSerializerOptions BatchUploadSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly VerifactuApiClient _apiClient = apiClient;
    private readonly ILogger<IndexModel> _logger = logger;
    private readonly ITenantContext _tenantContext = tenantContext;

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

    public bool IsNoVerifactuTenant { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        await EnsureTenantTypeAsync().ConfigureAwait(false);
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

    private async Task EnsureTenantTypeAsync()
    {
        try
        {
            var systemType = await _tenantContext.GetSystemTypeAsync().ConfigureAwait(false);
            IsNoVerifactuTenant = systemType.IsNoVerifactu();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo determinar el tipo de sistema del tenant.");
            IsNoVerifactuTenant = false;
        }
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        BatchIngestRequest? payload;
        try
        {
            payload = await Request.ReadFromJsonAsync<BatchIngestRequest>(BatchUploadSerializerOptions).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "El payload de carga de lote no es válido.");
            return BadRequest(new { message = "No se pudo interpretar la solicitud. Verifica el formato JSON." });
        }

        if (payload?.File is null
            || string.IsNullOrWhiteSpace(payload.File.FileName)
            || string.IsNullOrWhiteSpace(payload.File.PayloadBase64))
        {
            return BadRequest(new { message = "Debes proporcionar el archivo principal en formato Base64." });
        }

        payload.Metadata ??= new BatchIngestMetadata();
        payload.Metadata.TenantId ??= ResolveTenantId();
        payload.Metadata.Source ??= "Portal";

        if (payload.Metadata.Metadata is null)
        {
            payload.Metadata.Metadata = new Dictionary<string, string>();
        }

        try
        {
            var result = await _apiClient.IngestBatchAsync(payload).ConfigureAwait(false);

            var message = string.IsNullOrWhiteSpace(result?.Message)
                ? "Lote recibido correctamente. Utiliza el batchId para seguir el procesamiento."
                : result!.Message!;

            var statusCode = string.Equals(result?.Status, "Accepted", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status202Accepted
                : StatusCodes.Status200OK;

            return new JsonResult(new
            {
                batchId = result?.BatchId,
                status = result?.Status,
                jobId = result?.JobId,
                message
            })
            {
                StatusCode = statusCode
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo enviar el lote al backend.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "No se pudo enviar el lote al backend. Inténtalo de nuevo más tarde." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado procesando la carga del lote.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Ocurrió un error inesperado procesando el lote." });
        }
    }

    private string? ResolveTenantId()
    {
        var claim = User?.FindFirst("tenantId")?.Value ?? User?.FindFirst("custom:tenantId")?.Value;
        return string.IsNullOrWhiteSpace(claim) ? null : claim;
    }
}
