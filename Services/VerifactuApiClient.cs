using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Verifactu.Portal.Models;
using Verifactu.Portal.Options;

namespace Verifactu.Portal.Services;

public sealed class VerifactuApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly VerifactuApiOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public VerifactuApiClient(HttpClient httpClient, IAccessTokenProvider accessTokenProvider, IOptions<VerifactuApiOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
        _options = options?.Value ?? new VerifactuApiOptions();
        _httpContextAccessor = httpContextAccessor;

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        ApplyHeaders();
    }

    private async Task PrepareClientAsync()
    {
        var token = await _accessTokenProvider.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        ApplyHeaders();
    }

    private void ApplyHeaders()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var apiKeyFromClaims = GetClaimValue(user, "apiKey") ?? GetClaimValue(user, "custom:ApiKey");
        var tenantIdFromClaims = GetClaimValue(user, "tenantId") ?? GetClaimValue(user, "custom:tenantId");

        SetHeader("X-API-Key", apiKeyFromClaims ?? _options.ApiKey);
        SetHeader("X-App-Key", _options.AppKey);
        SetHeader("X-Tenant-Id", tenantIdFromClaims ?? _options.TenantId);
        SetHeader("X-CloudFront-Secret", _options.CloudFrontSecret);
    }

    private void SetHeader(string name, string? value)
    {
        _httpClient.DefaultRequestHeaders.Remove(name);

        if (!string.IsNullOrWhiteSpace(value))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
        }
    }

    private static string? GetClaimValue(ClaimsPrincipal? user, string claimType)
        => user?.FindFirst(claimType)?.Value;

    public async Task<PagedResult<BatchDto>> GetBatchesAsync(DateTime? from, DateTime? to, string? status, int page = 1, int pageSize = 50)
    {
        await PrepareClientAsync();
        using var response = await _httpClient.GetAsync("batches/pending");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return EmptyPage<BatchDto>(page, pageSize);
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BatchPendingResponse>(SerializerOptions).ConfigureAwait(false)
                      ?? new BatchPendingResponse();

        var batches = payload.Batches ?? new List<BatchDto>();

        if (from.HasValue)
        {
            var fromDate = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local);
            batches = batches
                .Where(b => b.CreatedAt.LocalDateTime >= fromDate)
                .ToList();
        }

        if (to.HasValue)
        {
            var inclusiveTo = DateTime.SpecifyKind(to.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
            batches = batches
                .Where(b => b.CreatedAt.LocalDateTime <= inclusiveTo)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            batches = batches
                .Where(b => string.Equals(b.Status, status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? 50 : pageSize;

        var totalCount = batches.Count;
        var skip = (page - 1) * pageSize;
        var items = batches.Skip(skip).Take(pageSize).ToList();

        return new PagedResult<BatchDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<BatchDetailDto?> GetBatchByIdAsync(string batchId)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return null;
        }

        using var response = await _httpClient.GetAsync($"batches/{batchId}/reports");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BatchDetailDto>(SerializerOptions).ConfigureAwait(false);
    }

    public async Task<RemoteUserStatusDto> GetRemoteUserStatusAsync()
    {
        await PrepareClientAsync();

        using var response = await _httpClient.GetAsync("remote/user").ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new RemoteUserStatusDto { RemoteCertificateEnabled = false };
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteUserStatusDto>(SerializerOptions).ConfigureAwait(false)
               ?? new RemoteUserStatusDto { RemoteCertificateEnabled = false };
    }

    public async Task DeleteBatchAsync(string batchId)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(batchId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(batchId));
        }

        using var response = await _httpClient.DeleteAsync($"batches/{Uri.EscapeDataString(batchId)}").ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<RemoteSubmissionResponseDto?> ResumeBatchAsync(string batchId, int maxItems = 10, int pollSeconds = 1, string? operationFilter = null)
    {
        await PrepareClientAsync();

        if (string.IsNullOrWhiteSpace(batchId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(batchId));
        }

        var payload = new
        {
            maxItems = Math.Max(1, maxItems),
            pollSeconds = Math.Max(0, pollSeconds),
            operation = string.IsNullOrWhiteSpace(operationFilter) ? null : operationFilter
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"remote/batches/{Uri.EscapeDataString(batchId)}/resume",
            payload,
            SerializerOptions).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RemoteSubmissionResponseDto>(SerializerOptions).ConfigureAwait(false);
    }

    public async Task<IList<InvoiceDto>> GetBatchItemsAsync(string batchId, string? status = null)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return Array.Empty<InvoiceDto>();
        }

        var query = new Dictionary<string, string?>
        {
            ["batchId"] = batchId
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            query["status"] = status;
        }

        var endpoint = QueryHelpers.AddQueryString("items", query!);

        using var response = await _httpClient.GetAsync(endpoint).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<InvoiceDto>();
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Array.Empty<InvoiceDto>();
        }

        try
        {
            var itemsResponse = JsonSerializer.Deserialize<BatchItemsResponseDto>(payload, SerializerOptions);
            if (itemsResponse?.Items is { Count: > 0 })
            {
                return itemsResponse.Items;
            }
        }
        catch (JsonException)
        {
            // Intentional fall-through to try parsing as a plain array.
        }

        try
        {
            var directItems = JsonSerializer.Deserialize<IList<InvoiceDto>>(payload, SerializerOptions);
            return directItems ?? Array.Empty<InvoiceDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<InvoiceDto>();
        }
    }

    public async Task<BatchItemResultSummary?> GetBatchItemResultSummaryAsync(string itemId)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        var path = $"items/{Uri.EscapeDataString(itemId)}/result";

        using var response = await _httpClient.GetAsync(path).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new BatchItemResultSummary();
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            string? facturaId = TryReadString(root, "facturaId");
            string? idempotencyKey = TryReadString(root, "idempotencyKey");

            if (string.IsNullOrWhiteSpace(facturaId) && root.ValueKind == JsonValueKind.Object && root.TryGetProperty("factura", out var facturaElement))
            {
                facturaId = TryReadString(facturaElement, "facturaId");
                if (string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    idempotencyKey = TryReadString(facturaElement, "idempotencyKey");
                }
            }

            return new BatchItemResultSummary
            {
                ItemId = itemId,
                FacturaId = facturaId,
                IdempotencyKey = idempotencyKey,
                RawPayload = payload
            };
        }
        catch (JsonException)
        {
            return new BatchItemResultSummary
            {
                ItemId = itemId,
                RawPayload = payload
            };
        }

        static string? TryReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (element.TryGetProperty(propertyName, out var valueElement))
            {
                return valueElement.ValueKind switch
                {
                    JsonValueKind.String => valueElement.GetString(),
                    JsonValueKind.Number => valueElement.TryGetInt64(out var number) ? number.ToString(CultureInfo.InvariantCulture) : valueElement.GetRawText(),
                    _ => valueElement.GetRawText()
                };
            }

            return null;
        }
    }

    public async Task<PagedResult<InvoiceDto>> GetInvoicesByBatchAsync(string batchId, int page = 1, int pageSize = 50)
    {
        await PrepareClientAsync();
        var report = await GetBatchByIdAsync(batchId).ConfigureAwait(false);
        if (report?.Items is not { Count: > 0 })
        {
            return EmptyPage<InvoiceDto>(page, pageSize);
        }

        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? 50 : pageSize;

        var totalCount = report.Items.Count;
        var skip = (page - 1) * pageSize;
        var items = report.Items.Skip(skip).Take(pageSize).ToList();

        return new PagedResult<InvoiceDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<InvoiceListResponseDto> GetInvoicesAsync(
        string? numeroSerie,
        string? emisor,
        string? nif,
        string? estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        int? pageSize,
        string? continuationToken)
    {
        await PrepareClientAsync();

        var query = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(numeroSerie))
        {
            query["numeroSerie"] = numeroSerie;
        }

        if (!string.IsNullOrWhiteSpace(emisor))
        {
            query["emisor"] = emisor;
        }

        if (!string.IsNullOrWhiteSpace(nif))
        {
            query["nif"] = nif;
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query["estado"] = estado;
        }

        if (fechaDesde.HasValue)
        {
            query["fechaDesde"] = fechaDesde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (fechaHasta.HasValue)
        {
            query["fechaHasta"] = fechaHasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (pageSize.HasValue && pageSize.Value > 0)
        {
            query["pageSize"] = pageSize.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            query["continuationToken"] = continuationToken;
        }

        var endpoint = query.Count > 0
            ? QueryHelpers.AddQueryString("facturas", query!)
            : "facturas";

        using var response = await _httpClient.GetAsync(endpoint).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new InvoiceListResponseDto();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvoiceListResponseDto>(SerializerOptions).ConfigureAwait(false)
               ?? new InvoiceListResponseDto();
    }

    public async Task<InvoiceDetailDto?> GetInvoiceDetailAsync(string facturaId, string? idempotencyKey)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(facturaId))
        {
            return null;
        }

        var path = $"facturas/{Uri.EscapeDataString(facturaId)}";

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            path = QueryHelpers.AddQueryString(path, "idempotencyKey", idempotencyKey);
        }

        using var response = await _httpClient.GetAsync(path).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvoiceDetailDto>(SerializerOptions).ConfigureAwait(false);
    }

    public async Task<InvoiceDocumentResponseDto?> GetInvoiceDocumentAsync(string facturaId, string documentType, string? idempotencyKey)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(facturaId) || string.IsNullOrWhiteSpace(documentType))
        {
            return null;
        }

        var normalizedType = documentType.Trim().ToLowerInvariant();
        var path = $"facturas/{Uri.EscapeDataString(facturaId)}/documentos/{normalizedType}";

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            path = QueryHelpers.AddQueryString(path, "idempotencyKey", idempotencyKey);
        }

        using var response = await _httpClient.GetAsync(path).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvoiceDocumentResponseDto>(SerializerOptions).ConfigureAwait(false);
    }

    public async Task<IList<ApiKeyDto>> GetApiKeysAsync()
    {
        await PrepareClientAsync();
        using var response = await _httpClient.GetAsync("settings/api-keys").ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Array.Empty<ApiKeyDto>();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IList<ApiKeyDto>>(SerializerOptions).ConfigureAwait(false)
               ?? Array.Empty<ApiKeyDto>();
    }

    public async Task<ApiKeyDto> CreateApiKeyAsync(string name)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        }

        using var response = await _httpClient.PostAsJsonAsync("settings/api-keys", new { name }, SerializerOptions).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<ApiKeyDto>(SerializerOptions).ConfigureAwait(false);
        if (created is null)
        {
            throw new InvalidOperationException("Unable to deserialize API key creation response.");
        }

        return created;
    }

    public async Task DeleteApiKeyAsync(string apiKeyId)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(apiKeyId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(apiKeyId));
        }

        using var response = await _httpClient.DeleteAsync($"settings/api-keys/{apiKeyId}").ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<ProfileDto> GetProfileAsync()
    {
        await PrepareClientAsync();
        using var response = await _httpClient.GetAsync("settings/profile").ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>(SerializerOptions).ConfigureAwait(false);
        if (profile is null)
        {
            throw new InvalidOperationException("No se pudo recuperar el perfil del inquilino actual.");
        }

        return profile;
    }

    public async Task UpdateProfileAsync(ProfileDto profile)
    {
        await PrepareClientAsync();
        ArgumentNullException.ThrowIfNull(profile);

        using var response = await _httpClient.PutAsJsonAsync("settings/profile", profile, SerializerOptions).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static PagedResult<T> EmptyPage<T>(int page, int pageSize) => new()
    {
        Items = Array.Empty<T>(),
        Page = Math.Max(1, page),
        PageSize = pageSize <= 0 ? 50 : pageSize,
        TotalCount = 0
    };
}
