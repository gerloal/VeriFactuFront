using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
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

    public async Task<BatchHistoryResponseDto> GetBatchHistoryAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();

        var query = new Dictionary<string, string?>();

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            query["from"] = fromUtc.ToString("o", CultureInfo.InvariantCulture);
        }

        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
            query["to"] = toUtc.ToString("o", CultureInfo.InvariantCulture);
        }

        var endpoint = "batches/history";
        if (query.Count > 0)
        {
            endpoint = QueryHelpers.AddQueryString(endpoint, query!);
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
        ApplyDefaultHeaders(message);

    #if DEBUG
        var historyUrl = ResolveRequestUrl(message.RequestUri);
        var historyApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, historyUrl, historyApiKey, "BATCH HISTORY QUERY").ConfigureAwait(false);
    #endif

        using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new BatchHistoryResponseDto();
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BatchHistoryResponseDto>(SerializerOptions, cancellationToken).ConfigureAwait(false)
               ?? new BatchHistoryResponseDto();
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

    public async Task<RemoteConsultationResponseDto?> ExecuteRemoteConsultationAsync(RemoteConsultationRequestDto request, CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();
        ArgumentNullException.ThrowIfNull(request);

        var consultationPath = "remote/consultas";
        using var message = new HttpRequestMessage(HttpMethod.Post, consultationPath)
        {
            Content = JsonContent.Create(request, options: SerializerOptions)
        };

        ApplyDefaultHeaders(message);

    #if DEBUG
        var consultationUrl = ResolveRequestUrl(message.RequestUri);
        var consultationApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, consultationUrl, consultationApiKey, "REMOTE CONSULTA AEAT").ConfigureAwait(false);
    #endif

        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RemoteConsultationResponseDto>(SerializerOptions, cancellationToken).ConfigureAwait(false)
               ?? new RemoteConsultationResponseDto();
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

        var resumePath = $"remote/batches/{Uri.EscapeDataString(batchId)}/resume";
        using var message = new HttpRequestMessage(HttpMethod.Post, resumePath)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };

        ApplyDefaultHeaders(message);

    #if DEBUG
        var resumeUrl = ResolveRequestUrl(message.RequestUri);
        var resumeApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, resumeUrl, resumeApiKey, "REMOTE BATCH RESUME").ConfigureAwait(false);
    #endif

        using var response = await _httpClient.SendAsync(message).ConfigureAwait(false);

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

    public async Task<BatchItemResultSummary?> GetBatchItemResultSummaryAsync(string batchId, string itemId)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(batchId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(batchId));
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        var path = $"batches/{Uri.EscapeDataString(batchId)}/items/{Uri.EscapeDataString(itemId)}";

        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyDefaultHeaders(message);

    #if DEBUG
        var itemUrl = ResolveRequestUrl(message.RequestUri);
        var itemApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, itemUrl, itemApiKey, "BATCH ITEM RESULT").ConfigureAwait(false);
    #endif

        using var response = await _httpClient.SendAsync(message).ConfigureAwait(false);

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

    public async Task<InvoiceExportResult?> DownloadInvoicesXmlAsync(DateTime from, DateTime to, string? docs = null, CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();

        var normalizedFrom = from.Date;
        var normalizedTo = to.Date;

        if (normalizedTo < normalizedFrom)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        var query = new Dictionary<string, string?>
        {
            ["from"] = normalizedFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = normalizedTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(docs))
        {
            query["docs"] = docs;
        }

        var endpoint = QueryHelpers.AddQueryString("facturas/export", query!);

        using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
        ApplyDefaultHeaders(message);

#if DEBUG
        var exportUrl = ResolveRequestUrl(message.RequestUri);
        var exportApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, exportUrl, exportApiKey, "FACTURAS XML EXPORT").ConfigureAwait(false);
#endif

        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var rawBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var headerContentType = response.Content.Headers.ContentType?.MediaType;
        var headerFileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName;

        if (IsLikelyBase64OrJson(rawBytes))
        {
            var textPayload = Encoding.UTF8.GetString(rawBytes);
            var decoded = ParseExportPayload(textPayload, headerFileName, headerContentType, normalizedFrom, normalizedTo);
            return new InvoiceExportResult(decoded.Content, decoded.ContentType, decoded.FileName);
        }

        var fallbackFileName = headerFileName ?? $"facturas-{normalizedFrom:yyyyMMdd}-{normalizedTo:yyyyMMdd}.zip";
        var fallbackContentType = string.IsNullOrWhiteSpace(headerContentType) ? "application/zip" : headerContentType;

        return new InvoiceExportResult(rawBytes, fallbackContentType, fallbackFileName.Trim('"'));
    }

    public async Task<InvoiceExportJobStatusDto> CreateInvoiceExportJobAsync(DateTime? from, DateTime? to, string? docs = null, CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();

        var payload = new Dictionary<string, object?>();
        if (from.HasValue)
        {
            payload["from"] = from.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (to.HasValue)
        {
            payload["to"] = to.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(docs))
        {
            payload["docs"] = docs;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "facturas/export/jobs")
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };

        ApplyDefaultHeaders(message);

    #if DEBUG
        var jobCreateUrl = ResolveRequestUrl(message.RequestUri);
        var jobCreateApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, jobCreateUrl, jobCreateApiKey, "FACTURAS EXPORT JOB CREATE").ConfigureAwait(false);
    #endif

        using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var job = await response.Content.ReadFromJsonAsync<InvoiceExportJobStatusDto>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        return job ?? new InvoiceExportJobStatusDto();
    }

    public async Task<InvoiceExportJobListResponseDto> ListInvoiceExportJobsAsync(
        int? pageSize = null,
        string? continuationToken = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();

        var query = new Dictionary<string, string?>();
        if (pageSize.HasValue && pageSize.Value > 0)
        {
            query["pageSize"] = pageSize.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            query["continuationToken"] = continuationToken;
        }

        if (includeDeleted)
        {
            query["includeDeleted"] = "true";
        }

        var endpoint = query.Count > 0
            ? QueryHelpers.AddQueryString("facturas/export/jobs", query!)
            : "facturas/export/jobs";

        using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
        ApplyDefaultHeaders(message);

#if DEBUG
        var listUrl = ResolveRequestUrl(message.RequestUri);
        var listApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, listUrl, listApiKey, "FACTURAS EXPORT JOB LIST").ConfigureAwait(false);
#endif

        using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new InvoiceExportJobListResponseDto();
        }

        response.EnsureSuccessStatusCode();

        return await ReadJsonOrLambdaBodyAsync<InvoiceExportJobListResponseDto>(response, cancellationToken).ConfigureAwait(false)
               ?? new InvoiceExportJobListResponseDto();
    }

    private static async Task<T?> ReadJsonOrLambdaBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var rawBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (rawBytes.Length == 0)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(rawBytes, SerializerOptions);
        }
        catch (JsonException)
        {
            // Some environments return Lambda proxy shape: { statusCode, headers, body, isBase64Encoded }
            using var doc = JsonDocument.Parse(rawBytes);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw;
            }

            var statusCode = 0;
            if (doc.RootElement.TryGetProperty("statusCode", out var statusProp) && statusProp.ValueKind == JsonValueKind.Number)
            {
                statusProp.TryGetInt32(out statusCode);
            }

            if (!doc.RootElement.TryGetProperty("body", out var bodyProp) || bodyProp.ValueKind != JsonValueKind.String)
            {
                throw;
            }

            var body = bodyProp.GetString();
            if (string.IsNullOrWhiteSpace(body))
            {
                return default;
            }

            var isBase64 = doc.RootElement.TryGetProperty("isBase64Encoded", out var base64Prop)
                           && base64Prop.ValueKind == JsonValueKind.True;

            if (statusCode >= 400)
            {
                string? message = null;
                try
                {
                    var errorJson = isBase64 ? Encoding.UTF8.GetString(Convert.FromBase64String(body)) : body;
                    using var errorDoc = JsonDocument.Parse(errorJson);
                    if (errorDoc.RootElement.ValueKind == JsonValueKind.Object
                        && errorDoc.RootElement.TryGetProperty("message", out var msgProp)
                        && msgProp.ValueKind == JsonValueKind.String)
                    {
                        message = msgProp.GetString();
                    }
                }
                catch
                {
                    // ignore
                }

                throw new HttpRequestException(message ?? $"Backend returned statusCode {statusCode}.");
            }

            if (isBase64)
            {
                var decoded = Convert.FromBase64String(body);
                return JsonSerializer.Deserialize<T>(decoded, SerializerOptions);
            }

            return JsonSerializer.Deserialize<T>(body, SerializerOptions);
        }
    }

    public async Task<InvoiceExportJobStatusDto> GetInvoiceExportJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(jobId));
        }

        var path = $"facturas/export/jobs/{Uri.EscapeDataString(jobId)}";
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyDefaultHeaders(message);

    #if DEBUG
        var jobStatusUrl = ResolveRequestUrl(message.RequestUri);
        var jobStatusApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, jobStatusUrl, jobStatusApiKey, "FACTURAS EXPORT JOB STATUS").ConfigureAwait(false);
    #endif

        using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var job = await ReadJsonOrLambdaBodyAsync<InvoiceExportJobStatusDto>(response, cancellationToken).ConfigureAwait(false);
        return job ?? new InvoiceExportJobStatusDto { JobId = jobId };
    }

    public async Task<InvoiceExportJobStatusDto> GetInvoiceExportJobStatusByUrlAsync(string statusUrl, CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(statusUrl))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(statusUrl));
        }

        if (!Uri.TryCreate(statusUrl, UriKind.RelativeOrAbsolute, out var statusUri))
        {
            throw new InvalidOperationException("El statusUrl recibido no es una URI válida.");
        }

        if (statusUri.IsAbsoluteUri)
        {
            var baseHost = _httpClient.BaseAddress?.Host;
            if (!string.IsNullOrWhiteSpace(baseHost)
                && !string.Equals(statusUri.Host, baseHost, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El statusUrl apunta a un host no permitido.");
            }
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, statusUri);
        ApplyDefaultHeaders(message);

    #if DEBUG
        var jobStatusUrl = ResolveRequestUrl(message.RequestUri);
        var jobStatusApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, jobStatusUrl, jobStatusApiKey, "FACTURAS EXPORT JOB STATUS (STATUSURL)").ConfigureAwait(false);
    #endif

        using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var job = await ReadJsonOrLambdaBodyAsync<InvoiceExportJobStatusDto>(response, cancellationToken).ConfigureAwait(false);
        return job ?? new InvoiceExportJobStatusDto();
    }

    public async Task DeleteInvoiceExportJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(jobId));
        }

        var path = $"facturas/export/jobs/{Uri.EscapeDataString(jobId)}";
        using var message = new HttpRequestMessage(HttpMethod.Delete, path);
        ApplyDefaultHeaders(message);

#if DEBUG
        var jobDeleteUrl = ResolveRequestUrl(message.RequestUri);
        var jobDeleteApiKey = TryGetHeaderValue(message.Headers, "X-API-Key");
        await LogLambdaTestFormatAsync(message, jobDeleteUrl, jobDeleteApiKey, "FACTURAS EXPORT JOB DELETE").ConfigureAwait(false);
#endif

        using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
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

    public async Task<ProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        await PrepareClientAsync();
        using var response = await _httpClient.GetAsync("settings/profile", cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>(SerializerOptions, cancellationToken).ConfigureAwait(false);
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

    private static bool IsLikelyBase64OrJson(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        var firstChar = (char)data[0];
        if (firstChar == '{' || firstChar == '[' || firstChar == '"')
        {
            return true;
        }

        foreach (var b in data)
        {
            var c = (char)b;

            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static InvoiceExportPayload ParseExportPayload(string payload, string? headerFileName, string? headerContentType, DateTime from, DateTime to)
    {
        var sanitized = payload.Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new InvalidOperationException("Empty export payload.");
        }

        if (sanitized.StartsWith("{", StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(sanitized);
            var root = document.RootElement;

            var base64Data = TryGetStringProperty(root, "content")
                           ?? TryGetStringProperty(root, "data")
                           ?? TryGetStringProperty(root, "payload")
                           ?? TryGetStringProperty(root, "zip")
                           ?? TryGetStringProperty(root, "zipBase64")
                           ?? (root.ValueKind == JsonValueKind.String ? root.GetString() : null);

            if (string.IsNullOrWhiteSpace(base64Data))
            {
                throw new InvalidOperationException("Export payload does not contain Base64 content.");
            }

            var content = DecodeBase64(base64Data);
            var resolvedContentType = headerContentType
                                      ?? TryGetStringProperty(root, "contentType")
                                      ?? "application/zip";

            var resolvedFileName = headerFileName
                                   ?? TryGetStringProperty(root, "fileName")
                                   ?? $"facturas-{from:yyyyMMdd}-{to:yyyyMMdd}.zip";

            return new InvoiceExportPayload(content, resolvedContentType, resolvedFileName.Trim('"'));
        }

        if (sanitized.StartsWith("\"", StringComparison.Ordinal) && sanitized.EndsWith("\"", StringComparison.Ordinal) && sanitized.Length >= 2)
        {
            sanitized = sanitized[1..^1];
        }

        var decodedBytes = DecodeBase64(sanitized);
        var contentType = string.IsNullOrWhiteSpace(headerContentType) ? "application/zip" : headerContentType;
        var fileName = headerFileName ?? $"facturas-{from:yyyyMMdd}-{to:yyyyMMdd}.zip";

        return new InvoiceExportPayload(decodedBytes, contentType, fileName.Trim('"'));
    }

    private static byte[] DecodeBase64(string value)
    {
        var sanitized = value.Replace("\r", string.Empty, StringComparison.Ordinal)
                             .Replace("\n", string.Empty, StringComparison.Ordinal)
                             .Trim();

        if (sanitized.Length == 0)
        {
            throw new InvalidOperationException("Export payload is empty.");
        }

        try
        {
            return Convert.FromBase64String(sanitized);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Export payload is not valid Base64.", ex);
        }
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private sealed record InvoiceExportPayload(byte[] Content, string ContentType, string FileName);

    private static PagedResult<T> EmptyPage<T>(int page, int pageSize) => new()
    {
        Items = Array.Empty<T>(),
        Page = Math.Max(1, page),
        PageSize = pageSize <= 0 ? 50 : pageSize,
        TotalCount = 0
    };

    private void ApplyDefaultHeaders(HttpRequestMessage message)
    {
        foreach (var header in _httpClient.DefaultRequestHeaders)
        {
            message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private Uri ResolveRequestUrl(Uri? requestUri)
    {
        if (requestUri is { IsAbsoluteUri: true })
        {
            return requestUri;
        }

        if (_httpClient.BaseAddress is not null)
        {
            return new Uri(_httpClient.BaseAddress, requestUri?.ToString() ?? string.Empty);
        }

        var relative = requestUri?.ToString() ?? string.Empty;
        return Uri.TryCreate(relative, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(new Uri("https://localhost"), relative);
    }

    private static string? TryGetHeaderValue(HttpHeaders headers, string key)
    {
        if (headers.TryGetValues(key, out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private static async Task LogLambdaTestFormatAsync(
        HttpRequestMessage request,
        Uri url,
        string? apiKey,
        string operationType,
        bool encodeBodyAsBase64 = false)
    {
        Console.WriteLine($"\n🔥 === COPIA {operationType} PARA LAMBDA TEST TOOL ===");

        var pathFromUrl = url.AbsolutePath;
        var headersJson = new StringBuilder();
        headersJson.Append("{\n");

        if (request.Content?.Headers is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                if (header.Key == "Content-Type")
                {
                    headersJson.Append($"    \"{header.Key}\": \"{string.Join(", ", header.Value)}\",\n");
                }
            }
        }

        foreach (var header in request.Headers)
        {
            headersJson.Append($"    \"{header.Key}\": \"{string.Join(", ", header.Value)}\",\n");
        }

#if DEBUG
        headersJson.Append("    \"X-CloudFront-Secret\": \"…\",\n");
#endif

        if (headersJson.Length > 3)
        {
            headersJson.Length -= 2;
            headersJson.Append("\n");
        }

        headersJson.Append("  }");

        var requestBodyRaw = request.Content is not null
            ? await request.Content.ReadAsStringAsync().ConfigureAwait(false)
            : string.Empty;

        var isBase64 = false;
        string requestBody;

        if (encodeBodyAsBase64 && !string.IsNullOrEmpty(requestBodyRaw))
        {
            requestBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(requestBodyRaw));
            isBase64 = true;
        }
        else
        {
            requestBody = requestBodyRaw.Replace("\"", "\\\"");
        }

        var requestApiKey = apiKey ?? "tu-api-key-aqui";

        var bodyLine = isBase64
            ? $"  \"isBase64Encoded\": true,\n  \"body\": \"{requestBody}\""
            : $"  \"body\": \"{requestBody}\"";

        var trimmedQuery = url.Query.StartsWith("?") ? url.Query[1..] : url.Query;
        var rawQueryEscaped = string.IsNullOrWhiteSpace(trimmedQuery) ? null : trimmedQuery.Replace("\"", "\\\"");

        string? queryParametersJson = null;
        if (!string.IsNullOrEmpty(url.Query))
        {
            var parsed = QueryHelpers.ParseQuery(url.Query);
            if (parsed.Count > 0)
            {
                var dict = parsed.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Count > 0 ? kvp.Value[^1] : string.Empty,
                    StringComparer.OrdinalIgnoreCase);
                queryParametersJson = JsonSerializer.Serialize(dict);
            }
        }

        var lambdaBuilder = new StringBuilder();
        lambdaBuilder.Append("{");
        lambdaBuilder.Append($"\n  \"httpMethod\": \"{request.Method.Method}\",");
        lambdaBuilder.Append($"\n  \"path\": \"{pathFromUrl}\"");

        if (rawQueryEscaped is not null)
        {
            lambdaBuilder.Append($",\n  \"rawQueryString\": \"{rawQueryEscaped}\"");
        }

        if (queryParametersJson is not null)
        {
            lambdaBuilder.Append($",\n  \"queryStringParameters\": {queryParametersJson}");
        }

        lambdaBuilder.Append($",\n  \"headers\": {headersJson},\n{bodyLine},\n  \"requestContext\": {{\n    \"identity\": {{\n      \"apiKey\": \"{requestApiKey}\"\n    }}\n  }}\n}}");

        Console.WriteLine(lambdaBuilder.ToString());
        Console.WriteLine($"=== FIN {operationType} LAMBDA TEST TOOL ===\n");
    }
}
