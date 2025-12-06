using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class InvoiceListResponseDto
{
    [JsonPropertyName("items")]
    public List<InvoiceListItemDto> Items { get; init; } = new();

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; init; }
}

public sealed class InvoiceListItemDto
{
    [JsonPropertyName("facturaId")]
    public string FacturaId { get; init; } = string.Empty;

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; init; }

    [JsonPropertyName("tenantId")]
    public string TenantId { get; init; } = string.Empty;

    [JsonPropertyName("emisorNif")]
    public string EmisorNif { get; init; } = string.Empty;

    [JsonPropertyName("emisorNombre")]
    public string? EmisorNombre { get; init; }

    [JsonPropertyName("numeroSerie")]
    public string NumeroSerie { get; init; } = string.Empty;

    [JsonPropertyName("fechaExpedicion")]
    public DateTime? FechaExpedicion { get; init; }

    [JsonPropertyName("tipoOperacion")]
    public string? TipoOperacion { get; init; }

    [JsonPropertyName("estadoDocumental")]
    public string? EstadoDocumental { get; init; }

    [JsonPropertyName("estadoRegistroAeat")]
    public string? EstadoRegistroAeat { get; init; }

    [JsonPropertyName("codigoAeat")]
    public int? CodigoAeat { get; init; }

    [JsonPropertyName("mensajeAeat")]
    public string? MensajeAeat { get; init; }

    [JsonPropertyName("aeatReferencia")]
    public string? AeatReferencia { get; init; }

    [JsonPropertyName("ultimaActualizacion")]
    public DateTimeOffset? UltimaActualizacion { get; init; }

    [JsonPropertyName("envioAeat")]
    public DateTimeOffset? EnvioAeat { get; init; }

    [JsonPropertyName("respuestaAeat")]
    public DateTimeOffset? RespuestaAeat { get; init; }

    [JsonPropertyName("documentos")]
    public InvoiceDocumentPointersDto Documentos { get; init; } = new();
}

public sealed class InvoiceDocumentPointersDto
{
    [JsonPropertyName("prefirmaKey")]
    public string? PrefirmaKey { get; init; }

    [JsonPropertyName("firmadaKey")]
    public string? FirmadaKey { get; init; }

    [JsonPropertyName("aeatRequestKey")]
    public string? AeatRequestKey { get; init; }

    [JsonPropertyName("aeatResponseKey")]
    public string? AeatResponseKey { get; init; }

    [JsonPropertyName("aeatSummaryKey")]
    public string? AeatSummaryKey { get; init; }
}
