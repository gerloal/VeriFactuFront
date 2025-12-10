using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class RemoteUserStatusDto
{
    public string TenantId { get; init; } = string.Empty;

    public bool RemoteCertificateEnabled { get; init; }

    [JsonPropertyName("idEmisorFactura")]
    public string? IdEmisorFactura { get; init; }

    [JsonPropertyName("nombreRazon")]
    public string? NombreRazon { get; init; }

    [JsonPropertyName("nombreEmisorFactura")]
    public string? NombreEmisorFactura { get; init; }
}
