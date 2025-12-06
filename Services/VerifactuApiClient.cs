using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Verifactu.Portal.Models;

namespace Verifactu.Portal.Services;

public sealed class VerifactuApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public VerifactuApiClient(HttpClient httpClient, IAccessTokenProvider accessTokenProvider, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;

        var baseUrl = configuration["VerifactuApi:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient.BaseAddress = new Uri(baseUrl);
        }
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
    }

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
