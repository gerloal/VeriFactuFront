using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class InvoiceExportJobStatusDto
{
    [JsonPropertyName("jobId")]
    public string? JobId { get; init; }

    [JsonPropertyName("statusUrl")]
    public string? StatusUrl { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; init; }

    [JsonPropertyName("downloadUrlExpiresAt")]
    [JsonConverter(typeof(UnixEpochDateTimeOffsetConverter))]
    public DateTimeOffset? DownloadUrlExpiresAt { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("skippedInvoices")]
    public int? SkippedInvoices { get; init; }

    [JsonExtensionData]
    public IDictionary<string, object?>? ExtensionData { get; init; }
}
