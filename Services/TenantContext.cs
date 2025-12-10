using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Verifactu.Portal.Models;

namespace Verifactu.Portal.Services;

public interface ITenantContext
{
    Task<TenantSystemType> GetSystemTypeAsync(CancellationToken cancellationToken = default);
    Task<ProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default);
}

public sealed class TenantContext : ITenantContext
{
    private readonly VerifactuApiClient _apiClient;
    private readonly ILogger<TenantContext> _logger;
    private ProfileDto? _profile;
    private bool _profileLoaded;
    private TenantSystemType? _cachedSystemType;

    public TenantContext(VerifactuApiClient apiClient, ILogger<TenantContext> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<ProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        if (_profileLoaded)
        {
            return _profile;
        }

        try
        {
            _profile = await _apiClient.GetProfileAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo recuperar el perfil del tenant para determinar el tipo de sistema.");
            _profile = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado recuperando el perfil del tenant.");
            _profile = null;
        }
        finally
        {
            _profileLoaded = true;
        }

        return _profile;
    }

    public async Task<TenantSystemType> GetSystemTypeAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSystemType.HasValue)
        {
            return _cachedSystemType.Value;
        }

        var profile = await GetProfileAsync(cancellationToken).ConfigureAwait(false);
        var systemType = profile?.SystemType ?? TenantSystemType.Unknown;
        _cachedSystemType = systemType;
        return systemType;
    }
}
