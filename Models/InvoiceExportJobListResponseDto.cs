using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class InvoiceExportJobListResponseDto
{
    [JsonPropertyName("items")]
    public List<InvoiceExportJobListItemDto> Items { get; init; } = new();

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; init; }
}

public sealed class InvoiceExportJobListItemDto
{
    [JsonPropertyName("jobId")]
    public string JobId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(UnixEpochDateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    [JsonConverter(typeof(UnixEpochDateTimeOffsetConverter))]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("to")]
    public string? To { get; init; }

    [JsonPropertyName("docs")]
    public string? Docs { get; init; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonExtensionData]
    public IDictionary<string, object?>? ExtensionData { get; init; }
}
