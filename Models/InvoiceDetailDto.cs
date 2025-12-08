using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class InvoiceDetailDto
{
    [JsonPropertyName("facturaId")]
    public string FacturaId { get; init; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; init; } = string.Empty;

    [JsonPropertyName("emisorNif")]
    public string EmisorNif { get; init; } = string.Empty;

    [JsonPropertyName("emisorNombre")]
    public string? EmisorNombre { get; init; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; init; }

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

    [JsonPropertyName("ledger")]
    public LedgerChainSnapshotDto? Ledger { get; init; }

    [JsonPropertyName("idempotency")]
    public IdempotencySnapshotDto Idempotency { get; init; } = new();

    [JsonPropertyName("timeline")]
    public List<InvoiceTimelineEventDto> Timeline { get; init; } = new();

    [JsonPropertyName("documentos")]
    public InvoiceDocumentPointersDto Documentos { get; init; } = new();

    [JsonPropertyName("qrUrl")]
    public string? QrUrl { get; init; }

    [JsonPropertyName("qrPngBase64")]
    public string? QrPngBase64 { get; init; }

    [JsonPropertyName("qrS3Key")]
    public string? QrS3Key { get; init; }
}

public sealed class LedgerChainSnapshotDto
{
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }

    [JsonPropertyName("hashAnterior")]
    public string? HashAnterior { get; init; }

    [JsonPropertyName("numeroSerieAnterior")]
    public string? NumeroSerieAnterior { get; init; }

    [JsonPropertyName("fechaExpedicionAnterior")]
    public string? FechaExpedicionAnterior { get; init; }

    [JsonPropertyName("primerRegistro")]
    public string? PrimerRegistro { get; init; }

    [JsonPropertyName("fechaCreacionRegistro")]
    public DateTimeOffset? FechaCreacionRegistro { get; init; }

    [JsonPropertyName("estadoRegistro")]
    public string? EstadoRegistro { get; init; }

    [JsonPropertyName("codigoError")]
    public string? CodigoError { get; init; }

    [JsonPropertyName("descripcionError")]
    public string? DescripcionError { get; init; }

    [JsonPropertyName("xmlKey")]
    public string? XmlKey { get; init; }

    [JsonPropertyName("responseS3Key")]
    public string? ResponseS3Key { get; init; }

    [JsonPropertyName("cancelXmlKey")]
    public string? CancelXmlKey { get; init; }

    [JsonPropertyName("estadoRegistroTimestamp")]
    public DateTimeOffset? EstadoRegistroTimestamp { get; init; }

    [JsonPropertyName("cancelTimestamp")]
    public DateTimeOffset? CancelTimestamp { get; init; }

    [JsonPropertyName("qrKey")]
    public string? QrKey { get; init; }
}

public sealed class IdempotencySnapshotDto
{
    [JsonPropertyName("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonPropertyName("estadoDocumento")]
    public string? EstadoDocumento { get; init; }

    [JsonPropertyName("requestHash")]
    public string RequestHash { get; init; } = string.Empty;

    [JsonPropertyName("creadoEn")]
    public DateTimeOffset? CreadoEn { get; init; }

    [JsonPropertyName("completadoEn")]
    public DateTimeOffset? CompletadoEn { get; init; }

    [JsonPropertyName("canceladoEn")]
    public DateTimeOffset? CanceladoEn { get; init; }

    [JsonPropertyName("ultimaActualizacion")]
    public DateTimeOffset? UltimaActualizacion { get; init; }

    [JsonPropertyName("incorrectoEn")]
    public DateTimeOffset? IncorrectoEn { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("s3BasePath")]
    public string? S3BasePath { get; init; }

    [JsonPropertyName("aeatResponseKey")]
    public string? AeatResponseKey { get; init; }

    [JsonPropertyName("aeatReferencia")]
    public string? AeatReferencia { get; init; }

    [JsonPropertyName("aeatStatusCode")]
    public int? AeatStatusCode { get; init; }

    [JsonPropertyName("aeatMensaje")]
    public string? AeatMensaje { get; init; }
}

public sealed class InvoiceTimelineEventDto
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; init; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; init; } = string.Empty;

    [JsonPropertyName("ocurrioEn")]
    public DateTimeOffset OcurrioEn { get; init; }

    [JsonPropertyName("datosExtra")]
    public string? DatosExtra { get; init; }
}

public sealed class InvoiceDocumentResponseDto
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("tipoDocumento")]
    public string TipoDocumento { get; init; } = string.Empty;
}
